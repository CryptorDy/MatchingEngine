using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    /// <summary>
    /// Performs matching of orders
    /// </summary>
    public class MatchingPool : BackgroundService
    {
        public readonly string _pairCode;
        private readonly BufferBlock<Order> _newOrdersBuffer = new BufferBlock<Order>();
        private readonly List<Order> _orders = new List<Order>();

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CurrenciesCache _currenciesCache;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly IDealEndingService _dealEndingService;
        private readonly ILiquidityDeletedOrdersKeeper _liquidityDeletedOrdersKeeper;
        private readonly LiquidityExpireBlocksHandler _liquidityExpireBlocksHandler;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        public MatchingPool(
            IServiceScopeFactory serviceScopeFactory,
            CurrenciesCache currenciesService,
            OrdersMatcher ordersMatcher,
            MarketDataHolder marketDataHolder,
            IDealEndingService dealEndingService,
            ILiquidityDeletedOrdersKeeper liquidityDeletedOrdersKeeper,
            LiquidityExpireBlocksHandler liquidityExpireBlocksHandler,
            IOptions<AppSettings> settings,
            ILogger<MatchingPool> logger,
            string currencyPairCode,
            List<Order> activeOrders)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _currenciesCache = currenciesService;
            _ordersMatcher = ordersMatcher;
            _marketDataHolder = marketDataHolder;
            _dealEndingService = dealEndingService;
            _liquidityDeletedOrdersKeeper = liquidityDeletedOrdersKeeper;
            _liquidityExpireBlocksHandler = liquidityExpireBlocksHandler;
            _settings = settings;
            _logger = logger;

            _pairCode = currencyPairCode;

            _orders = activeOrders;
            SendOrdersToMarketData();
        }

        public Order GetPoolOrder(Guid id)
        {
            return _orders.FirstOrDefault(_ => _.Id == id);
        }

        private async Task UpdateDatabase(TradingDbContext context, List<Order> modifiedOrders, List<Deal> newDeals)
        {
            if (modifiedOrders.Count == 0 && newDeals.Count == 0)
            {
                return;
            }

            if (modifiedOrders.Count > 0)
            {
                _logger.LogDebug($"Updating {modifiedOrders.Count} orders");
                var dbOrdersDict = (await context.LoadDbOrders(modifiedOrders)).ToDictionary(_ => _.Id, _ => _);

                foreach (var order in modifiedOrders)
                {
                    OrderEventType eventType;
                    if (!dbOrdersDict.TryGetValue(order.Id, out var dbOrder))
                    {
                        // only save copy of imported order (after external trade), not the initial imported order
                        if (!order.IsLocal && order.Fulfilled == 0)
                        {
                            continue;
                        }

                        // create if external or FromInnerBot and wasn't created yet
                        if (dbOrder == null && (!order.IsLocal || order.ClientType == ClientType.DealsBot))
                        {
                            eventType = OrderEventType.Create;
                            dbOrder = await context.AddOrder(order, true, eventType);
                            continue;
                        }
                        if (dbOrder == null)
                        {
                            throw new Exception($"Couldn't find in DB: {order}");
                        }
                    }

                    if (order.Fulfilled > dbOrder.Fulfilled)
                    {
                        eventType = OrderEventType.Fulfill;
                    }
                    else if (order.Blocked > 0 && dbOrder.Blocked == 0)
                    {
                        eventType = OrderEventType.Block;
                        _liquidityExpireBlocksHandler.Add(order.Id, _pairCode);
                    }
                    else if (order.Blocked == 0 && dbOrder.Blocked > 0)
                    {
                        eventType = OrderEventType.Unblock;
                        _liquidityExpireBlocksHandler.Remove(order.Id);
                    }
                    else
                    {
                        _logger.LogError($"Unknown OrderEventType. \n dbOrder:{dbOrder} \n order:{order}");
                        eventType = OrderEventType.Fulfill;
                    }
                    string orderDealIds = string.Join(',',
                        newDeals.Where(_ => _.BidId == order.Id || _.AskId == order.Id).Select(_ => _.DealId));

                    dbOrder.Fulfilled = order.Fulfilled;
                    dbOrder.Blocked = order.Blocked;
                    await context.UpdateOrder(dbOrder, false, eventType, orderDealIds);
                }
            }

            if (newDeals.Count > 0)
            {
                _logger.LogDebug($"Saved {newDeals.Count} new deals: \n{string.Join("\n", newDeals)}");
                context.Deals.AddRange(newDeals);
                context.DealCopies.AddRange(newDeals.Select(_ => new DealCopy(_)));
            }

            await context.SaveChangesAsync();
        }

        private async Task ReportData(TradingDbContext context, List<Order> modifiedOrders, List<Deal> newDeals)
        {
            try
            {
                // checks for correct finishing state
                foreach (var order in modifiedOrders)
                {
                    if (!order.IsActive && (order.AvailableAmount != 0 || order.Blocked != 0))
                    {
                        _logger.LogWarning($"CompletedOrder has incorrect state: {order}");
                    }
                }

                lock (_orders)
                {
                    SendOrdersToMarketData(); // send even if no orders matched, because we need to display new order
                }

                if (newDeals.Count > 0)
                {
                    var dealGuids = newDeals.Select(md => md.DealId).ToList();
                    var dbDeals = context.Deals.Include(d => d.Ask).Include(d => d.Bid)
                        .Where(d => dealGuids.Contains(d.DealId))
                        .ToDictionary(d => d.DealId, d => d);

                    foreach (var item in newDeals)
                    {
                        var dbDeal = dbDeals[item.DealId];
                        await SendDealToMarketData(dbDeal);
                    }
                    _ = _dealEndingService.SendDeals();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
            }
        }

        public async Task<SaveExternalOrderResult> UpdateExternalOrder(ExternalCreatedOrder createdOrder)
        {
            _logger.LogInformation($"UpdateExternalOrder() start: {createdOrder}");
            if (_currenciesCache.GetCurrencyPair(createdOrder.CurrencyPairCode) != null)
                createdOrder.Fulfilled = Math.Round(createdOrder.Fulfilled, _currenciesCache.GetAmountDigits(createdOrder.CurrencyPairCode));
            var modifiedOrders = new List<Order>();
            var newDeals = new List<Deal>();

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                // Find previously matched orders
                var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                Order bid, ask;
                lock (_orders)
                {
                    bid = _orders.FirstOrDefault(x => x.Id == Guid.Parse(createdOrder.TradingBidId));
                    ask = _orders.FirstOrDefault(x => x.Id == Guid.Parse(createdOrder.TradingAskId));
                }
                if (bid == null)
                {
                    bid = await db.Bids.FirstOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingBidId));
                }

                if (ask == null)
                {
                    ask = await db.Asks.FirstOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingAskId));
                }
                // todo handle cases with null bid or ask

                var (matchedLocalOrder, matchedImportedOrder) = createdOrder.IsBid ? (bid, ask) : (ask, bid);

                bool isFullfillmentError = false;
                if (matchedLocalOrder.IsCanceled
                    || matchedLocalOrder.Fulfilled + createdOrder.Fulfilled > matchedLocalOrder.Amount
                    || matchedLocalOrder.Exchange != Exchange.Local
                    || matchedLocalOrder.CurrencyPairCode != createdOrder.CurrencyPairCode)
                {
                    _logger.LogError($"UpdateExternalOrder() error for {matchedLocalOrder}: " +
                        $"order is wrong Or total fullfilled {matchedLocalOrder.Fulfilled + createdOrder.Fulfilled} is bigger than amount");
                    isFullfillmentError = true;
                }
                // Update matched orders
                lock (_orders)
                {
                    if (matchedImportedOrder != null)
                        matchedImportedOrder.Blocked = 0;

                    matchedLocalOrder.Blocked = 0;
                    if (!isFullfillmentError)
                    {
                        matchedLocalOrder.Fulfilled += createdOrder.Fulfilled;
                    }
                    modifiedOrders.Add(matchedLocalOrder);
                    _orders.RemoveAll(o => !o.IsActive);
                }

                Order newImportedOrder = null;
                if (createdOrder.Fulfilled > 0 && !isFullfillmentError)
                {
                    // Create a separate completed order for the matched part of the imported order
                    newImportedOrder = new Order()
                    {
                        Id = Guid.NewGuid(),
                        IsBid = !createdOrder.IsBid,
                        CurrencyPairCode = createdOrder.CurrencyPairCode,
                        DateCreated = matchedImportedOrder?.DateCreated ?? DateTimeOffset.UtcNow,
                        Exchange = createdOrder.Exchange,
                        Price = createdOrder.MatchingEngineDealPrice,
                        Amount = createdOrder.Fulfilled,
                        Fulfilled = createdOrder.Fulfilled,
                        ClientType = ClientType.LiquidityBot,
                        UserId = $"{createdOrder.Exchange}_matched"
                    };
                    modifiedOrders.Add(newImportedOrder);

                    // Create deal
                    newDeals.Add(new Deal(matchedLocalOrder, newImportedOrder,
                        createdOrder.MatchingEngineDealPrice, createdOrder.Fulfilled));
                }

                await UpdateDatabase(db, modifiedOrders, newDeals);
                await ReportData(db, modifiedOrders, newDeals);

                var result = new SaveExternalOrderResult
                {
                    NewExternalOrderId = newDeals.Count > 0 ? newImportedOrder.Id.ToString() : null,
                    CreatedDealId = newDeals.FirstOrDefault()?.DealId.ToString() ?? null,
                };
                _logger.LogInformation($"UpdateExternalOrder() finished. {result}\n newImportedOrder:{newImportedOrder}");
                return result;
            }
        }

        public void SendOrdersToMarketData()
        {
            try
            {
                _marketDataHolder.SetOrders(_pairCode, _orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending orders to marketdata");
            }
        }

        private async Task SendDealToMarketData(Deal deal)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var marketDataService = scope.ServiceProvider.GetRequiredService<MarketDataService>();
                    await marketDataService.SendNewDeal(deal.GetDealResponse());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        private async Task Process(Order newOrder)
        {
            _logger.LogDebug("Detected new order, Matching process started");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            List<Order> modifiedOrders;
            List<Deal> newDeals;

            lock (_orders) //no access to pool (for removing) while matching is performed
            {
                DateTime start = DateTime.UtcNow;
                (modifiedOrders, newDeals) = _ordersMatcher.Match(_orders, newOrder);
                if (newOrder.IsActive)
                {
                    _orders.Add(newOrder);
                }
                _orders.RemoveAll(o => !o.IsActive);
                _logger.LogDebug($"Matching completed: {(DateTime.UtcNow - start).TotalMilliseconds}ms; " +
                    $"Orders in pool: {_orders.Count};");
                CheckOrderbookIntersection(newOrder);
                UpdateDatabase(context, modifiedOrders, newDeals).Wait();
            }
            await ReportData(context, modifiedOrders, newDeals);
        }

        public void AppendOrder(Order order)
        {
            _newOrdersBuffer.Post(order);
        }

        public CancelOrderResponse CancelOrder(Guid orderId)
        {
            _logger.LogDebug($"DeleteOrder() start. id:{orderId}");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            lock (_orders)
            {
                var dbOrder = context.GetOrder(orderId).Result;
                if (dbOrder == null)
                {
                    _logger.LogDebug("DeleteOrder() Not found.");
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
                }
                _logger.LogDebug($"DeleteOrder() {dbOrder}");

                if (dbOrder.Blocked > 0)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.LiquidityBlocked, Order = dbOrder };
                }
                if (dbOrder.IsCanceled)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyCanceled, Order = dbOrder };
                }
                if (dbOrder.Fulfilled >= dbOrder.Amount)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyFilled, Order = dbOrder };
                }

                var order = GetPoolOrder(orderId);
                _orders.Remove(order);
                SendOrdersToMarketData();

                dbOrder.IsCanceled = true;
                context.UpdateOrder(dbOrder, true, OrderEventType.Cancel).Wait();
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Success, Order = dbOrder };
            }
        }

        #region Liquidity

        public async Task UnblockOrders(List<Guid> orderIds)
        {
            if (orderIds?.Count == 0)
            {
                return;
            }
            List<Order> ordersToUnblock;
            lock (_orders)
            {
                ordersToUnblock = _orders.Where(_ => orderIds.Contains(_.Id) && _.Blocked > 0).ToList();
                ordersToUnblock.ForEach(_ => _.Blocked = 0);
            }

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                await UpdateDatabase(context, ordersToUnblock, new List<Deal>());
            }
            lock (_orders)
            {
                SendOrdersToMarketData();
            }
        }

        public void SaveLiquidityImportUpdate(ImportUpdateDto dto)
        {
            var date1 = DateTimeOffset.UtcNow;
            // delete
            RemoveOrders(dto.OrdersToDelete.Select(_ => Guid.Parse(_.ActionId)).ToList());

            var date2 = DateTimeOffset.UtcNow;
            // update
            lock (_orders)
            {
                foreach (var order in dto.OrdersToUpdate)
                {
                    var o = _orders.FirstOrDefault(_ => _.Id == Guid.Parse(order.ActionId));
                    if (o != null)
                    {
                        if (o.IsLocal)
                        {
                            throw new ArgumentException($"Local exchange changes are forbidden. {o}");
                        }

                        o.Amount = order.Amount;
                        o.DateCreated = order.DateCreated;
                    }
                }
            }

            var date3 = DateTimeOffset.UtcNow;
            // add
            var ordersToAdd = dto.OrdersToAdd.Select(_ => _.GetOrder()).ToList();
            ordersToAdd.ForEach(_ => AppendOrder(_));

            lock (_orders)
            {
                var bidPrice = _orders.Where(_ => _.IsBid && _.CurrencyPairCode == Constants.DebugCurrencyPair)
                    .OrderByDescending(_ => _.Price).FirstOrDefault()?.Price;
                _logger.LogDebug($"SaveLiquidityImportUpdate() top {Constants.DebugCurrencyPair} bid: {bidPrice}");

                SendOrdersToMarketData();

                var date4 = DateTimeOffset.UtcNow;
                if (dto.OrdersToAdd.Count > 100 || new Random().Next(100) == 0)
                    _logger.LogDebug($"SaveLiquidityImportUpdate()  Orders came:{dto.OrdersToAdd.Count}/{dto.OrdersToUpdate.Count}/{dto.OrdersToDelete.Count}. " +
                        $"Dates: {date1:HH:mm:ss.fff} {date2:HH:mm:ss.fff} {date3:HH:mm:ss.fff} {date4:HH:mm:ss.fff}. Orders in memory:{_orders.Count}");
            }
        }

        public void RemoveOrders(IEnumerable<Guid> ids)
        {
            _liquidityDeletedOrdersKeeper.AddRange(ids);
            lock (_orders)
            {
                var ordersToRemove = _orders.Where(_ => ids.Contains(_.Id)).ToList();
                if (ordersToRemove.Any(_ => _.IsLocal))
                {
                    throw new ArgumentException("Local exchange changes are forbidden");
                }

                int removedOrdersCount = _orders.RemoveAll(_ => ids.Contains(_.Id));
                if (removedOrdersCount > 0)
                {
                    SendOrdersToMarketData();
                }
            }
        }

        public void RemoveLiquidityOrderbook(Exchange exchange)
        {
            if (exchange == Exchange.Local)
            {
                throw new ArgumentException("Local exchange changes are forbidden");
            }

            lock (_orders)
            {
                int removedOrdersCount = _orders.RemoveAll(_ => _.Exchange == exchange);
                if (removedOrdersCount > 0)
                {
                    SendOrdersToMarketData();
                }
            }
        }

        public void RemoveLiquidityOldOrders()
        {
            lock (_orders)
            {
                var minDate = DateTimeOffset.UtcNow.AddMinutes(-_settings.Value.ImportedOrdersExpirationMinutes);
                int removedOrdersCount = _orders.RemoveAll(_ => !_.IsLocal && _.Blocked == 0 && _.DateCreated < minDate);
                if (removedOrdersCount > 0)
                {
                    _logger.LogInformation($"RemoveLiquidityOldOrders() expired {removedOrdersCount} orders");
                    SendOrdersToMarketData();
                }
            }
        }

        #endregion Liquidity

        public void RemoveOldInnerBotOrders()
        {
            lock (_orders)
            {
                int removedOrdersCount = _orders.RemoveAll(_ => _.ClientType == ClientType.DealsBot && _.DateCreated < DateTimeOffset.UtcNow.AddSeconds(-50));
                if (removedOrdersCount > 0)
                {
                    SendOrdersToMarketData();
                }
            }
        }

        private long _orderbookIntersectionLogsCounter = 0;

        /// <summary>
        /// Log when bids are bigger than asks
        /// </summary>
        private void CheckOrderbookIntersection(Order newOrder)
        {
            if (_orderbookIntersectionLogsCounter++ % 100 != 0) // only show 1 in 100 logs
            {
                return;
            }

            var currencyPairOrders = _orders.Where(o => o.CurrencyPairCode == newOrder.CurrencyPairCode);
            var biggestBid = currencyPairOrders.Where(o => o.IsBid).OrderByDescending(_ => _.Price).FirstOrDefault();
            var lowestAsk = currencyPairOrders.Where(o => !o.IsBid).OrderBy(_ => _.Price).FirstOrDefault();
            if (biggestBid != null && lowestAsk != null && biggestBid.Price > lowestAsk.Price)
            {
                _logger.LogError($"\n!!!!!!! bids are bigger than asks.");
                _logger.LogError($"newOrder: {newOrder}\nbiggestBid: {biggestBid}\nlowestAsk: {lowestAsk}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _newOrdersBuffer.OutputAvailableAsync(cancellationToken))
                    {
                        continue;
                    }

                    var newOrder = await _newOrdersBuffer.ReceiveAsync(cancellationToken);

                    if (!newOrder.IsLocal && _liquidityDeletedOrdersKeeper.Contains(newOrder.Id))
                    {
                        _logger.LogInformation($"Skipped order processing (already deleted): {newOrder}");
                    }
                    else
                    {
                        await Process(newOrder);
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Trading processing error");
                }
            }
        }
    }
}

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

namespace MatchingEngine.Services
{
    /// <summary>
    /// Performs matching of orders
    /// </summary>
    public class MatchingPool : BackgroundService
    {
        private readonly BufferBlock<Order> _newOrdersBuffer = new BufferBlock<Order>();
        private readonly List<Order> _orders = new List<Order>();

        private DealEndingSender _dealEndingSender;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ICurrenciesService _currenciesService;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly ILiquidityDeletedOrdersKeeper _liquidityDeletedOrdersKeeper;
        private LiquidityExpireBlocksWatcher _liquidityExpireBlocksWatcher;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        /// <summary>
        /// DI constructor
        /// </summary>
        /// <param name="marketDataService"></param>
        /// <param name="logger"></param>
        /// <param name="ordersMatcher"></param>
        /// <param name="serviceScopeFactory"></param>
        public MatchingPool(
            IServiceScopeFactory serviceScopeFactory,
            ICurrenciesService currenciesService,
            OrdersMatcher ordersMatcher,
            MarketDataService marketDataService,
            MarketDataHolder marketDataHolder,
            ILiquidityDeletedOrdersKeeper liquidityDeletedOrdersKeeper,
            IOptions<AppSettings> settings,
            ILogger<MatchingPool> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _currenciesService = currenciesService;
            _ordersMatcher = ordersMatcher;
            _marketDataService = marketDataService;
            _marketDataHolder = marketDataHolder;
            _liquidityDeletedOrdersKeeper = liquidityDeletedOrdersKeeper;
            _settings = settings;
            _logger = logger;

            UnblockAllDbOrders().Wait(); // Remove liquidity blocks created before app restart
        }

        public void SetServices(DealEndingSender dealEndingSender, LiquidityExpireBlocksWatcher liquidityExpireBlocksWatcher)
        {
            _dealEndingSender = dealEndingSender;
            _liquidityExpireBlocksWatcher = liquidityExpireBlocksWatcher;
            LoadOrders(); // load orders from db after all services are set
        }

        private void LoadOrders()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                var dbBids = context.Bids.AsNoTracking().Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount).ToList(); // todo change back to IsActive
                var dbAsks = context.Asks.AsNoTracking().Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount).ToList();
                var dbOrders = dbBids.Cast<Order>().Union(dbAsks).ToList();
                lock (_orders)
                {
                    _orders.AddRange(dbOrders);
                    SendOrdersToMarketData();
                }
            }
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
                _logger.LogInformation($"Updating {modifiedOrders.Count} orders");
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
                        _liquidityExpireBlocksWatcher.Add(order.Id);
                    }
                    else if (order.Blocked == 0 && dbOrder.Blocked > 0)
                    {
                        eventType = OrderEventType.Unblock;
                        _liquidityExpireBlocksWatcher.Remove(order.Id);
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
                _logger.LogInformation($"Saved {newDeals.Count} new deals: \n{string.Join("\n", newDeals)}");
                context.Deals.AddRange(newDeals);
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
                    _dealEndingSender.SendDeals();
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
            createdOrder.Fulfilled = Math.Round(createdOrder.Fulfilled, _currenciesService.GetAmountDigits(createdOrder.CurrencyPairCode));
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
                    {
                        matchedImportedOrder.Blocked = 0;
                    }

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
                    NewExternalOrderId = newDeals.Count > 0 ? newImportedOrder.ToString() : null,
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
                _marketDataHolder.SendOrders(_orders);
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
                await _marketDataService.SendNewDeal(deal.GetDealResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        private async Task Process(Order newOrder)
        {
            _logger.LogDebug("Detected new order, Matching process started");
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                List<Order> modifiedOrders;
                List<Deal> newDeals;

                lock (_orders) //no access to pool (for removing) while matching is performed
                {
                    DateTime start = DateTime.UtcNow;
                    (modifiedOrders, newDeals) = _ordersMatcher.Match(_orders, newOrder);
                    _orders.Add(newOrder);
                    _orders.RemoveAll(o => !o.IsActive);
                    _logger.LogDebug($"Matching completed: {(DateTime.UtcNow - start).TotalMilliseconds}ms; " +
                        $"Orders in pool: {_orders.Count};");
                    CheckOrderbookIntersection(newOrder);
                }
                await UpdateDatabase(context, modifiedOrders, newDeals);
                await ReportData(context, modifiedOrders, newDeals);
            }
        }

        public void AppendOrder(Order order)
        {
            _newOrdersBuffer.Post(order);
        }

        public async Task RemoveOrder(Guid id)
        {
            lock (_orders)
            {
                var order = _orders.FirstOrDefault(_ => _.Id == id);
                if (order != null)
                {
                    _orders.Remove(order);
                    SendOrdersToMarketData();
                }
            }
        }

        #region Liquidity

        private async Task UnblockAllDbOrders()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                var blockedBids = await context.Bids.Where(_ => _.Blocked > 0).ToListAsync();
                var blockedAsks = await context.Asks.Where(_ => _.Blocked > 0).ToListAsync();
                foreach (Order order in blockedBids.Cast<Order>().Union(blockedAsks))
                {
                    order.Blocked = 0;
                }
                await context.SaveChangesAsync();
            }
        }

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

        public async Task SaveLiquidityImportUpdate(ImportUpdateDto dto)
        {
            // delete
            await RemoveOrders(dto.OrdersToDelete.Select(_ => Guid.Parse(_.ActionId)).ToList());

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

            // add
            var ordersToAdd = dto.OrdersToAdd.Select(_ => _.GetOrder()).ToList();
            ordersToAdd.ForEach(_ => AppendOrder(_));

            lock (_orders)
            {
                var bidPrice = _orders.Where(_ => _.IsBid && _.CurrencyPairCode == Constants.DebugCurrencyPair)
                    .OrderByDescending(_ => _.Price).FirstOrDefault()?.Price;
                _logger.LogInformation($"SaveLiquidityImportUpdate() top {Constants.DebugCurrencyPair} bid: {bidPrice}");

                SendOrdersToMarketData();
            }
        }

        public async Task RemoveOrders(IEnumerable<Guid> ids)
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

        public void RemoveLiquidityOrderbook(Exchange exchange, string currencyPairCode)
        {
            if (exchange == Exchange.Local)
            {
                throw new ArgumentException("Local exchange changes are forbidden");
            }

            lock (_orders)
            {
                int removedOrdersCount = _orders.RemoveAll(_ => _.Exchange == exchange && _.CurrencyPairCode == currencyPairCode);
                if (removedOrdersCount > 0)
                {
                    SendOrdersToMarketData();
                }
            }
        }

        public async Task RemoveLiquidityOldOrders()
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

        public async Task RemoveOldInnerBotOrders()
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Trading processing error");
                }
            }
        }
    }
}

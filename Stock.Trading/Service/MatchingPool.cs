using AutoMapper;
using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    /// <summary>
    /// Performs matching of orders
    /// </summary>
    public class MatchingPool : BackgroundService
    {
        public readonly string _pairCode;
        private readonly BufferBlock<PoolAction> _actionsBuffer = new();
        private readonly ConcurrentDictionary<Guid, MatchingOrder> _orders = new();

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CurrenciesCache _currenciesCache;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly IDealEndingService _dealEndingService;
        private readonly ILiquidityDeletedOrdersKeeper _deletedOrdersKeeper;
        private readonly LiquidityExpireBlocksHandler _liquidityExpireBlocksHandler;
        private readonly IOptions<AppSettings> _settings;
        private readonly IMapper _mapper;
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
            IMapper mapper,
            ILogger<MatchingPool> logger,
            string currencyPairCode,
            List<MatchingOrder> activeOrders)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _currenciesCache = currenciesService;
            _ordersMatcher = ordersMatcher;
            _marketDataHolder = marketDataHolder;
            _dealEndingService = dealEndingService;
            _deletedOrdersKeeper = liquidityDeletedOrdersKeeper;
            _liquidityExpireBlocksHandler = liquidityExpireBlocksHandler;
            _settings = settings;
            _mapper = mapper;
            _logger = logger;

            _pairCode = currencyPairCode;

            activeOrders.ForEach(order => { _orders[order.Id] = order; });
            SendOrdersToMarketData();
        }

        public MatchingOrder GetPoolOrder(Guid id)
        {
            return _orders.GetValueOrDefault(id, null);
        }

        private async Task UpdateDatabase(TradingDbContext context, List<MatchingOrder> modifiedOrders, List<Deal> newDeals,
            List<MatchingExternalTrade> newLiquidityTrades = null)
        {
            if (modifiedOrders.Count == 0 && newDeals.Count == 0 && (newLiquidityTrades == null || newLiquidityTrades.Count == 0))
                return;

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
                            continue;

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
                    await context.UpdateOrder(dbOrder, false, eventType, dealIds: orderDealIds);
                }
            }

            if (newDeals.Count > 0)
            {
                _logger.LogDebug($"Saved {newDeals.Count} new deals: \n{string.Join("\n", newDeals)}");
                context.Deals.AddRange(newDeals);
                context.DealCopies.AddRange(newDeals.Select(_ => new DealCopy(_)));
            }
            if (newLiquidityTrades != null)
                context.ExternalTrades.AddRange(newLiquidityTrades);

            await context.SaveChangesAsync();
        }

        private async Task ReportData(TradingDbContext context, List<MatchingOrder> modifiedOrders, List<Deal> newDeals)
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

        public async Task<SaveExternalOrderResult> SaveExternalOrderResult(ExternalTrade externalTrade)
        {
            _logger.LogInformation($"UpdateExternalOrder() start: {externalTrade}");
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

            var modifiedOrders = new List<MatchingOrder>();
            var newDeals = new List<Deal>();
            var liquidityTrade = await db.ExternalTrades.FirstOrDefaultAsync(_ => _.Id == externalTrade.Id);
            MatchingOrder newImportedOrder = null;
            lock (_orders)
            {
                if (_currenciesCache.GetCurrencyPair(externalTrade.CurrencyPairCode) != null)
                    externalTrade.Fulfilled = Math.Round(externalTrade.Fulfilled, _currenciesCache.GetAmountDigits(externalTrade.CurrencyPairCode));

                // Find previously matched orders
                MatchingOrder bid = _orders.GetValueOrDefault(Guid.Parse(externalTrade.TradingBidId), null);
                MatchingOrder ask = _orders.GetValueOrDefault(Guid.Parse(externalTrade.TradingAskId), null);
                if (bid == null)
                {
                    bid = db.Bids.FirstOrDefault(_ => _.Id == Guid.Parse(externalTrade.TradingBidId));
                    _logger.LogWarning($"UpdateExternalOrder() couldn't find bid of {externalTrade} in pool. In DB: {bid}");
                }
                if (ask == null)
                {
                    ask = db.Asks.FirstOrDefault(_ => _.Id == Guid.Parse(externalTrade.TradingAskId));
                    _logger.LogWarning($"UpdateExternalOrder() couldn't find ask of {externalTrade} in pool. In DB: {ask}");
                }

                var (matchedLocalOrder, matchedImportedOrder) = externalTrade.IsBid ? (bid, ask) : (ask, bid);

                bool isTooMuchExternallyFulfilled =
                    matchedLocalOrder.Fulfilled + externalTrade.Fulfilled > matchedLocalOrder.Amount;

                bool isFullfillmentError = matchedLocalOrder.IsCanceled
                    || !matchedLocalOrder.IsLocal
                    || isTooMuchExternallyFulfilled;
                if (isFullfillmentError)
                    _logger.LogError($"UpdateExternalOrder() error: invalid {externalTrade} for {matchedLocalOrder}");

                // Update matched orders
                if (matchedImportedOrder != null)
                    matchedImportedOrder.Blocked = 0;

                matchedLocalOrder.Blocked = 0;
                if (!isFullfillmentError)
                {
                    matchedLocalOrder.Fulfilled += externalTrade.Fulfilled;
                }
                modifiedOrders.Add(matchedLocalOrder);
                RemoveNotActivePoolOrders();

                if (externalTrade.Fulfilled > 0 && !isFullfillmentError)
                {
                    // Create a separate completed order for the matched part of the imported order
                    newImportedOrder = new MatchingOrder()
                    {
                        Id = Guid.NewGuid(),
                        IsBid = !externalTrade.IsBid,
                        CurrencyPairCode = externalTrade.CurrencyPairCode,
                        DateCreated = matchedImportedOrder?.DateCreated ?? DateTimeOffset.UtcNow,
                        Exchange = externalTrade.Exchange,
                        Price = externalTrade.MatchingEngineDealPrice,
                        Amount = externalTrade.Fulfilled,
                        Fulfilled = externalTrade.Fulfilled,
                        ClientType = ClientType.LiquidityBot,
                        UserId = $"{externalTrade.Exchange}_matched"
                    };
                    modifiedOrders.Add(newImportedOrder);

                    // Create deal
                    var deal = new Deal(matchedLocalOrder, newImportedOrder,
                        externalTrade.MatchingEngineDealPrice, externalTrade.Fulfilled);
                    newDeals.Add(deal);

                    if (liquidityTrade == null) // in case service stopped before liquidityTrade was saved
                    {
                        liquidityTrade = new MatchingExternalTrade(bid, ask)
                        {
                            Id = externalTrade.Id,
                        };
                        db.ExternalTrades.Add(liquidityTrade);
                        _logger.LogWarning($"SaveExternalOrderResult liquidityTrade had to be recreated {liquidityTrade}");
                    }
                    liquidityTrade.DealId = deal.DealId;
                    // TODO change status?

                    db.SaveChanges();
                }
                UpdateDatabase(db, modifiedOrders, newDeals).Wait();
            }
            await ReportData(db, modifiedOrders, newDeals);

            var result = new SaveExternalOrderResult
            {
                NewExternalOrderId = newDeals.Count > 0 ? newImportedOrder.Id : null,
                CreatedDealId = newDeals.FirstOrDefault()?.DealId ?? null,
            };
            _logger.LogInformation($"UpdateExternalOrder() finished. {result}\n newImportedOrder:{newImportedOrder}");
            return result;
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
                using var scope = _serviceScopeFactory.CreateScope();
                var marketDataService = scope.ServiceProvider.GetRequiredService<MarketDataService>();
                await marketDataService.SendNewDeal(deal.GetDealResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        private async Task ProcessNewOrder(MatchingOrder newOrder)
        {
            _logger.LogDebug($"Process() started {newOrder}");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            List<MatchingOrder> modifiedOrders;
            List<Deal> newDeals;
            List<MatchingExternalTrade> liquidityTrades;

            lock (_orders) // no access to pool (for removing) while matching is performed
            {
                DateTime start = DateTime.UtcNow;
                if (newOrder.ClientType != ClientType.LiquidityBot && newOrder.ClientType != ClientType.DealsBot)
                    context.AddOrder(newOrder, true, OrderEventType.Create).Wait();

                (modifiedOrders, newDeals, liquidityTrades) = _ordersMatcher.Match(_orders.Values, newOrder);
                if (newOrder.IsActive)
                {
                    _orders[newOrder.Id] = newOrder;
                }
                RemoveNotActivePoolOrders();
                _logger.LogDebug($"Matching completed: {(DateTime.UtcNow - start).TotalMilliseconds}ms; " +
                    $"new order: {newOrder}, Orders in pool: {_orders.Count};");
                LogOrderbookIntersections(newOrder);
                UpdateDatabase(context, modifiedOrders, newDeals).Wait();
            }
            await ReportData(context, modifiedOrders, newDeals);
        }

        private void CancelOrder(PoolAction cancelAction)
        {
            if (cancelAction.ActionType != PoolActionType.CancelOrder)
                throw new ArgumentException("WrongActionType");
            _logger.LogDebug($"CancelOrder() start. id:{cancelAction.OrderId}");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            Models.MatchingOrder dbOrder;
            lock (_orders)
            {
                dbOrder = context.GetOrder(cancelAction.OrderId).Result;
                string errorText = null;
                if (dbOrder == null)
                    errorText = $"order '{cancelAction.OrderId}' not found";
                else if (dbOrder.Blocked > 0 && !cancelAction.ToForce)
                    errorText = CancelOrderResponseStatus.LiquidityBlocked.ToString();
                else if (dbOrder.Fulfilled >= dbOrder.Amount)
                    errorText = CancelOrderResponseStatus.AlreadyFilled.ToString();

                _logger.LogDebug($"CancelOrder() {dbOrder}");
                if (errorText.HasValue())
                {
                    _logger.LogWarning($"CancelOrder {errorText} for {dbOrder}");
                    return;
                }
                var order = GetPoolOrder(cancelAction.OrderId);
                _orders.TryRemove(order.Id, out _);
                _deletedOrdersKeeper.Add(cancelAction.OrderId);

                dbOrder.IsCanceled = true;
                dbOrder.Blocked = 0;
                context.UpdateOrder(dbOrder, true, OrderEventType.Cancel).Wait();
            }
            _dealEndingService.SendOrderCancellings();
            SendOrdersToMarketData();
            _logger.LogDebug($"CancelOrder() finished {dbOrder}");
        }

        /// <summary>Remove orders from pool and notify MarketData</summary>
        /// <param name="ids">ids to remove</param>
        /// <param name="clientType">optionally check that only certain clientType is removed</param>
        private void RemovePoolOrders(IEnumerable<Guid> ids, ClientType? clientType = null)
        {
            _deletedOrdersKeeper.AddRange(ids);
            int countDeleted = 0;
            foreach (Guid id in ids)
            {
                if (_orders.TryRemove(id, out var order))
                {
                    countDeleted++;
                    if (clientType.HasValue && order.ClientType != clientType.Value)
                        throw new ArgumentException($"Invalid clientType, expected {clientType}: {order}");
                }
            }
            if (countDeleted > 0)
                SendOrdersToMarketData();
        }

        private void RemoveNotActivePoolOrders()
        {
            var ids = _orders.Values.Where(_ => !_.IsActive).Select(_ => _.Id).ToList();
            RemovePoolOrders(ids);
        }

        #region Liquidity

        private async Task UnblockOrder(Guid orderId)
        {
            lock (_orders)
            {
                var order = GetPoolOrder(orderId);
                if (order == null || order.Blocked == 0)
                    return;
                order.Blocked = 0;
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                UpdateDatabase(context, new List<MatchingOrder>() { order }, new List<Deal>()).Wait();
            }
            SendOrdersToMarketData();
        }

        public void SaveLiquidityImportedOrders(ImportUpdateDto dto)
        {
            var date1 = DateTimeOffset.UtcNow;
            // delete
            foreach (var order in dto.OrdersToDelete)
                EnqueuePoolAction(PoolActionType.RemoveLiquidityOrder, Guid.Parse(order.ActionId));

            var date2 = DateTimeOffset.UtcNow;
            // update
            foreach (var orderModel in dto.OrdersToUpdate)
            {
                var order = _mapper.Map<MatchingOrder>(orderModel.GetOrder());
                EnqueuePoolAction(PoolActionType.UpdateLiquidityOrder, order.Id, order);
            }

            var date3 = DateTimeOffset.UtcNow;
            // add
            var ordersToAdd = dto.OrdersToAdd.Select(_ => _.GetOrder()).ToList();
            ordersToAdd.ForEach(_ => EnqueueCreateOrderAction(_mapper.Map<MatchingOrder>(_)));

            if (_pairCode == Constants.DebugCurrencyPair)
            {
                var bidPrice = _orders.Values.Where(_ => _.IsBid).OrderByDescending(_ => _.Price).FirstOrDefault()?.Price;
                _logger.LogDebug($"SaveLiquidityImportUpdate() top {Constants.DebugCurrencyPair} bid: {bidPrice}");
                var date4 = DateTimeOffset.UtcNow;
                if (dto.OrdersToAdd.Count > 100 || new Random().Next(100) == 0)
                    _logger.LogDebug($"SaveLiquidityImportUpdate()  Orders came:{dto.OrdersToAdd.Count}/{dto.OrdersToUpdate.Count}/{dto.OrdersToDelete.Count}. " +
                        $"Dates: {date1:HH:mm:ss.fff} {date2:HH:mm:ss.fff} {date3:HH:mm:ss.fff} {date4:HH:mm:ss.fff}. Orders in memory:{_orders.Count}");
            }
            SendOrdersToMarketData();
        }

        private void UpdateLiquidityOrder(MatchingOrder order)
        {
            lock (_orders)
            {
                var poolOrder = _orders.GetValueOrDefault(order.Id, null);
                if (poolOrder == null)
                {
                    EnqueueCreateOrderAction(order);
                    return;
                }
                if (poolOrder.IsLocal)
                    throw new ArgumentException($"Local exchange changes are forbidden. {poolOrder}");
                poolOrder.Amount = order.Amount;
                poolOrder.DateCreated = order.DateCreated;
            }
            SendOrdersToMarketData();
        }

        public void RemoveLiquidityOrderbook(Exchange exchange)
        {
            if (exchange == Exchange.Local)
                throw new ArgumentException("Local exchange changes are forbidden");
            var ids = _orders.Values.Where(_ => _.Exchange == exchange).Select(_ => _.Id).ToList();
            foreach (var id in ids)
                EnqueuePoolAction(PoolActionType.RemoveLiquidityOrder, id);
        }

        public void RemoveLiquidityOldOrders()
        {
            var minDate = DateTimeOffset.UtcNow.AddMinutes(-_settings.Value.ImportedOrdersExpirationMinutes);
            var ids = _orders.Values.Where(_ => !_.IsLocal && _.Blocked == 0 && _.DateCreated < minDate)
                .Select(_ => _.Id).ToList();
            if (ids.Count == 0)
                return;
            _logger.LogInformation($"RemoveLiquidityOldOrders() expired {ids.Count} orders");
            foreach (var id in ids)
                EnqueuePoolAction(PoolActionType.RemoveLiquidityOrder, id);
        }

        #endregion Liquidity

        public void RemoveOldInnerBotOrders()
        {
            var minDate = DateTimeOffset.UtcNow.AddSeconds(-50);
            var ids = _orders.Values.Where(_ => _.ClientType == ClientType.DealsBot && _.DateCreated < minDate)
                .Select(_ => _.Id).ToList();
            RemovePoolOrders(ids, ClientType.DealsBot);
        }

        private long _orderbookIntersectionLogsCounter = 0;

        /// <summary>
        /// Log when bids are bigger than asks
        /// </summary>
        private void LogOrderbookIntersections(MatchingOrder newOrder)
        {
            if (_orderbookIntersectionLogsCounter++ % 100 != 0) // only show 1 in 100 logs
                return;
            var biggestBid = _orders.Values.Where(o => o.IsBid).OrderByDescending(_ => _.Price).FirstOrDefault();
            var lowestAsk = _orders.Values.Where(o => !o.IsBid).OrderBy(_ => _.Price).FirstOrDefault();
            if (biggestBid != null && lowestAsk != null && biggestBid.Price > lowestAsk.Price)
            {
                _logger.LogError($"\n!!!!!!! bids are bigger than asks.");
                _logger.LogError($"newOrder: {newOrder}\nbiggestBid: {biggestBid}\nlowestAsk: {lowestAsk}");
            }
        }

        public void EnqueuePoolAction(PoolActionType actionType, Guid orderId, MatchingOrder order = null,
            bool toForce = false)
        {
            var action = new PoolAction
            {
                ActionType = actionType,
                OrderId = orderId,
                Order = order,
                ToForce = toForce,
            };
            _actionsBuffer.Post(action);
            if (_actionsBuffer.Count > 100 & new Random().Next(50) == 0)
                _logger.LogWarning($"{_pairCode} actionsBuffer count:{_actionsBuffer.Count}");
        }

        public void EnqueueCreateOrderAction(MatchingOrder order)
        {
            EnqueuePoolAction(PoolActionType.CreateOrder, order.Id, order);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            PoolAction newAction = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _actionsBuffer.OutputAvailableAsync(cancellationToken))
                        continue;
                    newAction = await _actionsBuffer.ReceiveAsync(cancellationToken);

                    if (newAction.ActionType == PoolActionType.CreateOrder)
                    {
                        var newOrder = newAction.Order;
                        if (_deletedOrdersKeeper.Contains(newOrder.Id))
                        {
                            if (newOrder.IsLocal)
                                _logger.LogInformation($"Skipped order processing (already deleted): {newOrder}");
                        }
                        else
                        {
                            await ProcessNewOrder(newOrder);
                        }
                    }
                    else if (newAction.ActionType == PoolActionType.CancelOrder)
                    {
                        CancelOrder(newAction);
                    }
                    else if (newAction.ActionType == PoolActionType.UpdateLiquidityOrder)
                    {
                        UpdateLiquidityOrder(newAction.Order);
                    }
                    else if (newAction.ActionType == PoolActionType.RemoveLiquidityOrder)
                    {
                        RemovePoolOrders(new List<Guid> { newAction.OrderId }, ClientType.LiquidityBot);
                    }
                    else if (newAction.ActionType == PoolActionType.AutoUnblock)
                    {
                        await UnblockOrder(newAction.OrderId);
                    }
                    else
                    {
                        _logger.LogCritical($"ExecuteAsync {_pairCode} unknown action {newAction}");
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{newAction}");
                }
            }
        }
    }
}

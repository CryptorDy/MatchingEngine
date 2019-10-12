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
        private readonly List<Guid> _liquidityDeletedOrderIds = new List<Guid>();

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly DealEndingService _dealEndingService;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        /// <summary>
        /// DI constructor
        /// </summary>
        /// <param name="marketDataService"></param>
        /// <param name="dealEndingService"></param>
        /// <param name="logger"></param>
        /// <param name="ordersMatcher"></param>
        /// <param name="serviceScopeFactory"></param>
        public MatchingPool(
            IServiceScopeFactory serviceScopeFactory,
            OrdersMatcher ordersMatcher,
            MarketDataService marketDataService,
            MarketDataHolder marketDataHolder,
            DealEndingService dealEndingService,
            IOptions<AppSettings> settings,
            ILogger<MatchingPool> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _ordersMatcher = ordersMatcher;
            _marketDataService = marketDataService;
            _marketDataHolder = marketDataHolder;
            _dealEndingService = dealEndingService;
            _settings = settings;
            _logger = logger;

            LoadOrders();
        }

        private void LoadOrders()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                var dbOrders = context.Bids.AsNoTracking().Where(a => a.IsActive).Cast<Order>()
                    .Union(context.Asks.AsNoTracking().Where(a => a.IsActive))
                    .ToList();
                foreach (var order in dbOrders.OrderBy(o => o.DateCreated))
                {
                    _newOrdersBuffer.Post(order);
                }
            }
        }

        private async Task UpdateDatabase(TradingDbContext context, List<Order> modifiedOrders, List<Models.Deal> newDeals)
        {
            if (modifiedOrders.Count > 0)
            {
                _logger.LogInformation($"Updating {modifiedOrders.Count} orders");
                var dbOrdersDict = (await context.GetOrders(modifiedOrders)).ToDictionary(_ => _.Id, _ => _);

                foreach (var order in modifiedOrders)
                {
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
                            dbOrder = await context.AddOrder(order, true);
                        }
                        if (dbOrder == null)
                        {
                            throw new Exception($"Couldn't find in DB: {order}");
                        }
                    }
                    dbOrder.Fulfilled = order.Fulfilled;
                    dbOrder.Blocked = order.Blocked;
                    await context.UpdateOrder(dbOrder, false);
                }
            }

            if (newDeals.Count > 0)
            {
                _logger.LogInformation($"Created {newDeals.Count} new deals");
            }

            context.Deals.AddRange(newDeals);
            context.SaveChanges();
        }

        private async Task ReportData(TradingDbContext db, List<Order> modifiedOrders, List<Models.Deal> newDeals)
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

                await SendOrdersToMarketData();
                var dealGuids = newDeals.Select(md => md.DealId).ToList();
                if (dealGuids.Count == 0)
                {
                    return;
                }

                var dbDeals = db.Deals.Include(d => d.Ask).Include(d => d.Bid)
                    .Where(d => dealGuids.Contains(d.DealId))
                    .ToDictionary(d => d.DealId, d => d);
                foreach (var item in newDeals)
                {
                    var dbDeal = dbDeals[item.DealId];
                    dbDeal.RemoveCircularDependency();
                    var t1 = Task.Run(async () => { await SendDealToMarketData(dbDeal); });
                    var t2 = Task.Run(async () => { await SendDealToDealEnding(dbDeal); });
                    Task.WaitAll(t1, t2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
            }
        }

        public async Task<SaveExternalOrderResult> UpdateExternalOrder(ExternalCreatedOrder createdOrder)
        {
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

                // Update matched orders
                lock (_orders)
                {
                    if (matchedImportedOrder != null)
                    {
                        matchedImportedOrder.Blocked = 0;
                    }

                    matchedLocalOrder.Blocked = 0;
                    matchedLocalOrder.Fulfilled += createdOrder.Fulfilled;
                    modifiedOrders.Add(matchedLocalOrder);
                    _orders.RemoveAll(o => !o.IsActive);
                }

                Order newImportedOrder = null;
                if (createdOrder.Fulfilled > 0)
                {
                    // Create a separate completed order for the matched part of the imported order
                    newImportedOrder = new Order()
                    {
                        Id = Guid.NewGuid(),
                        IsBid = !createdOrder.IsBid,
                        CurrencyPairCode = createdOrder.CurrencyPairCode,
                        DateCreated = DateTimeOffset.UtcNow,
                        Exchange = createdOrder.Exchange,
                        Price = createdOrder.MatchingEngineDealPrice,
                        Amount = createdOrder.Fulfilled,
                        Fulfilled = createdOrder.Fulfilled,
                        UserId = $"{createdOrder.Exchange}_matched"
                    };
                    modifiedOrders.Add(newImportedOrder);

                    // Create deal
                    newDeals.Add(new Deal(matchedLocalOrder, newImportedOrder,
                        createdOrder.MatchingEngineDealPrice, createdOrder.Fulfilled));
                }

                await UpdateDatabase(db, modifiedOrders, newDeals);
                await ReportData(db, modifiedOrders, newDeals);

                return new SaveExternalOrderResult
                {
                    NewExternalOrderId = newDeals.Count > 0 ? newImportedOrder.ToString() : null,
                    CreatedDealId = newDeals.FirstOrDefault()?.DealId.ToString() ?? null,
                };
            }
        }

        public async Task SendOrdersToMarketData()
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

        private async Task SendDealToDealEnding(Deal deal)
        {
            try
            {
                await _dealEndingService.SendDeal(deal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending deal to DealEnding");
            }
        }

        private async Task SendDealToMarketData(Deal deal)
        {
            try
            {
                await _marketDataService.SaveNewDeal(deal.GetDealResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        private async Task Process(Order newOrder)
        {
            _logger.LogInformation("Detected new order, Matching process started");
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
                    _logger.LogInformation($"Matching completed: {(DateTime.UtcNow - start).TotalMilliseconds}ms; " +
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
                }
            }
            await SendOrdersToMarketData();
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

            await SendOrdersToMarketData();
        }

        public async Task RemoveOrders(IEnumerable<Guid> ids)
        {
            lock (_liquidityDeletedOrderIds)
            {
                _liquidityDeletedOrderIds.AddRange(ids);
            }

            lock (_orders)
            {
                var ordersToRemove = _orders.Where(_ => ids.Contains(_.Id)).ToList();
                if (ordersToRemove.Any(_ => _.IsLocal))
                {
                    throw new ArgumentException("Local exchange changes are forbidden");
                }

                _orders.RemoveAll(_ => ids.Contains(_.Id));
            }
            await SendOrdersToMarketData();
        }

        private bool IsInLiquidityDeletedOrderIds(Guid id)
        {
            lock (_liquidityDeletedOrderIds)
            {
                return _liquidityDeletedOrderIds.Contains(id);
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
                _orders.RemoveAll(_ => _.Exchange == exchange && _.CurrencyPairCode == currencyPairCode);
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
                    Console.WriteLine($"RemoveLiquidityOldOrders() expired {removedOrdersCount} orders");
                }
            }
        }

        public async Task RemoveOldInnerBotOrders()
        {
            lock (_orders)
            {
                _orders.RemoveAll(_ => _.ClientType == ClientType.DealsBot && _.DateCreated < DateTimeOffset.UtcNow.AddSeconds(-50));
            }
            await SendOrdersToMarketData();
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

                    if (!newOrder.IsLocal && IsInLiquidityDeletedOrderIds(newOrder.Id))
                    {
                        Console.WriteLine($"Skipped order processing (already deleted): {newOrder}");
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

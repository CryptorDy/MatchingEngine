using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stock.Trading;
using Stock.Trading.Data;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Models.InnerTradingBot;
using Stock.Trading.Models.LiquidityImport;
using Stock.Trading.Responses;
using Stock.Trading.Service;
using Stock.Trading.Services;
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
                var dbOrders = context.BidsV2.AsNoTracking().Where(a => a.IsActive)
                    .Union(context.AsksV2.AsNoTracking().Where(a => a.IsActive))
                    .ToList();
                foreach (var order in dbOrders.OrderBy(o => o.DateCreated))
                {
                    _newOrdersBuffer.Post(order);
                }
            }
        }

        public CurrencyPairPrices GetCurrencyPairPrices(string currencyPairCode)
        {
            // todo use imported orders prices ?
            var orders = _orders
                .Where(_ => _.IsActive && _.CurrencyPairCode == currencyPairCode && !_.FromInnerTradingBot).ToList();
            return new CurrencyPairPrices
            {
                CurrencyPair = currencyPairCode,
                BidMax = orders.Where(_ => _.IsBid).Select(_ => _.Price).DefaultIfEmpty().Max(),
                AskMin = orders.Where(_ => !_.IsBid).Select(_ => _.Price).DefaultIfEmpty().Min(),
            };
        }

        private async Task UpdateDatabase(TradingDbContext context, List<Order> modifiedOrders, List<Models.Deal> newDeals)
        {
            if (modifiedOrders.Count > 0)
            {
                _logger.LogInformation($"Updating {modifiedOrders.Count} orders");
                var dbOrdersDict = (await context.GetDbOrders(modifiedOrders)).ToDictionary(_ => _.Id, _ => _);

                foreach (var order in modifiedOrders)
                {
                    if (!dbOrdersDict.TryGetValue(order.Id, out var dbOrder))
                    {
                        // create if external or FromInnerBot and wasn't created yet
                        if (dbOrder == null && (order.Exchange != Exchange.Local || order.FromInnerTradingBot))
                        {
                            dbOrder = order;
                            await context.AddOrder(dbOrder);
                        }
                        if (dbOrder == null)
                            throw new Exception($"Couldn't find in DB: {order}");
                    }
                    dbOrder.Fulfilled = order.Fulfilled;
                    dbOrder.Blocked = order.Blocked;
                }
            }

            if (newDeals.Count > 0)
                _logger.LogInformation($"Created {newDeals.Count} new deals");
            context.DealsV2.AddRange(newDeals);
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
                        _logger.LogWarning($"CompletedOrder has incorrect state: {order}");
                }

                var dealGuids = newDeals.Select(md => md.DealId).ToList();
                if (dealGuids.Count == 0) return;
                var dbDeals = db.DealsV2.Include(d => d.Ask).Include(d => d.Bid)
                    .Where(d => dealGuids.Contains(d.DealId))
                    .ToDictionary(d => d.DealId, d => d);
                foreach (var item in newDeals)
                {
                    var dbDeal = dbDeals[item.DealId];
                    var t1 = Task.Run(async () => { await SendOrdersToMarketData(); });
                    var t2 = Task.Run(async () => { await SendDealToMarketData(dbDeal); });
                    var t3 = Task.Run(async () => { await SendFeedToMarketData(dbDeal); });
                    var t4 = Task.Run(async () => { await SendDealToDealEnding(dbDeal); });
                    Task.WaitAll(t1, t2, t3, t4);
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
            var newDeals = new List<MatchingEngine.Models.Deal>();

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
                if (bid == null) bid = await db.BidsV2.FirstOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingBidId));
                if (ask == null) ask = await db.AsksV2.FirstOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingAskId));
                // todo handle cases with null bid or ask

                var (matchedLocalOrder, matchedImportedOrder) = createdOrder.IsBid ? (bid, ask) : (ask, bid);

                // Update matched orders
                lock (_orders)
                {
                    matchedLocalOrder.Blocked -= createdOrder.Amount;
                    matchedLocalOrder.Fulfilled += createdOrder.Fulfilled;
                    modifiedOrders.Add(matchedLocalOrder);
                    matchedImportedOrder.Blocked -= createdOrder.Amount;
                    _orders.RemoveAll(o => !o.IsActive);
                }

                Order newImportedOrder = null;
                if (createdOrder.Fulfilled > 0)
                {
                    // Create a separate completed order for the matched part of the imported order
                    newImportedOrder = new Order()
                    {
                        Id = Guid.NewGuid(),
                        IsBid = matchedImportedOrder.IsBid,
                        CurrencyPairCode = matchedImportedOrder.CurrencyPairCode,
                        DateCreated = matchedImportedOrder.DateCreated,
                        Exchange = matchedImportedOrder.Exchange,
                        Price = matchedImportedOrder.Price,
                        Amount = createdOrder.Fulfilled,
                        Fulfilled = createdOrder.Fulfilled,
                        UserId = $"{matchedImportedOrder.UserId}_matched"
                    };
                    modifiedOrders.Add(newImportedOrder);

                    // Create deal
                    newDeals.Add(new MatchingEngine.Models.Deal(matchedLocalOrder, newImportedOrder,
                        createdOrder.Price, createdOrder.Fulfilled));
                }

                await UpdateDatabase(db, modifiedOrders, newDeals);
                await ReportData(db, modifiedOrders, newDeals);

                return new SaveExternalOrderResult
                {
                    NewExternalOrderId = newDeals.Count > 0 ? newImportedOrder.ToString() : null,
                    CreatedDealId = newDeals.FirstOrDefault().DealId.ToString() ?? null,
                };
            }
        }

        public async Task SendOrdersToMarketData()
        {
            try
            {
                _marketDataHolder.SendOrders(null/* todo  _orders*/);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending orders to marketdata");
            }
        }

        private async Task SendDealToDealEnding(MatchingEngine.Models.Deal deal)
        {
            try
            {
                await _dealEndingService.SendDeal(deal.DealId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending deal to DealEnding");
            }
        }

        private async Task SendFeedToMarketData(MatchingEngine.Models.Deal deal)
        {
            try
            {
                MarketDataFeed feed = new MarketDataFeed
                {
                    Amount = deal.Price,
                    Volume = deal.Volume,
                    Date = deal.DateCreated.DateTime,
                    Currency = deal.Ask.CurrencyPairCode
                };
                await _marketDataService.PutData(feed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending feed to marketdata");
            }
        }

        private async Task SendDealToMarketData(MatchingEngine.Models.Deal deal)
        {
            try
            {
                var dealResponse = new DealResponse()
                {
                    DealId = deal.DealId,
                    Price = deal.Price,
                    Volume = deal.Volume,
                    DealDateUtc = deal.DateCreated.DateTime,
                    CurrencyPairId = deal.Ask.CurrencyPairCode,
                    BidId = deal.Bid.Id,
                    AskId = deal.Ask.Id,
                    UserBidId = deal.Bid.UserId,
                    UserAskId = deal.Ask.UserId,
                    IsBuy = deal.Bid.DateCreated > deal.Ask.DateCreated
                };
                await _marketDataService.SaveNewDeal(dealResponse);
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
                List<Models.Deal> newDeals;

                lock (_orders) //no access to pool (for removing) while matching is performed
                {
                    DateTime start = DateTime.UtcNow;
                    (modifiedOrders, newDeals) = _ordersMatcher.Match(_orders, newOrder);
                    _orders.Add(newOrder);
                    _orders.RemoveAll(o => !o.IsActive);
                    _logger.LogInformation($"Matching completed: {(DateTime.UtcNow - start).TotalMilliseconds}ms; " +
                        $"Orders in pool: {_orders.Count};");

                    // debug, find when bids are bigger than asks
                    var currencyPairOrders = _orders.Where(o => o.CurrencyPairCode == newOrder.CurrencyPairCode);
                    var biggestBid = currencyPairOrders.Where(o => o.IsBid).OrderByDescending(_ => _.Price).FirstOrDefault();
                    var lowestAsk = currencyPairOrders.Where(o => !o.IsBid).OrderBy(_ => _.Price).FirstOrDefault();
                    if (biggestBid != null && lowestAsk != null && biggestBid.Price > lowestAsk.Price)
                    {
                        _logger.LogError($"\n!!!!!!! bids are bigger than asks.");
                        _logger.LogError($"newOrder: {newOrder}\nbiggestBid: {biggestBid}\nlowestAsk: {lowestAsk}");
                    }
                    // end debug
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
                    _orders.Remove(order);
            }
            await SendOrdersToMarketData();
        }

        public async Task UpdateOrders(IEnumerable<Stock.Trading.Requests.AddRequestLiquidity> orders)
        {
            lock (_orders)
            {
                foreach (var order in orders)
                {
                    var o = _orders.FirstOrDefault(_ => _.Id == Guid.Parse(order.TradingOrderId));
                    if (o != null)
                    {
                        o.Amount = order.Amount;
                        o.DateCreated = order.OrderDateUtc;
                    }
                }
            }
            await SendOrdersToMarketData();
        }

        public async Task RemoveOrders(IEnumerable<Guid> ids)
        {
            lock (_orders)
            {
                _orders.RemoveAll(_ => ids.Contains(_.Id));
            }
            await SendOrdersToMarketData();
        }

        public void RemoveLiquidityOrderbook(Exchange exchange, string currencyPairCode)
        {
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
                int removedOrdersCount = _orders.RemoveAll(_ => _.Exchange != Exchange.Local && _.DateCreated < minDate);
                if (removedOrdersCount > 0)
                    Console.WriteLine($"RemoveLiquidityOldOrders() expired {removedOrdersCount} orders");
            }
        }

        public async Task RemoveOldInnerBotOrders()
        {
            lock (_orders)
            {
                _orders.RemoveAll(_ => _.FromInnerTradingBot && _.DateCreated < DateTimeOffset.UtcNow.AddSeconds(-7));
            }
            await SendOrdersToMarketData();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _newOrdersBuffer.OutputAvailableAsync(cancellationToken))
                        continue;
                    var newOrder = await _newOrdersBuffer.ReceiveAsync(cancellationToken);
                    await Process(newOrder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Trading processing error");
                }
            }
        }
    }
}

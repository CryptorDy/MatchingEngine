using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stock.Trading.Data;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Models.InnerTradingBot;
using Stock.Trading.Models.LiquidityImport;
using Stock.Trading.Requests;
using Stock.Trading.Responses;
using Stock.Trading.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stock.Trading.Service
{
    /// <summary>
    /// Performs matching of orders
    /// </summary>
    public class MatchingPool : BackgroundService
    {
        private readonly BufferBlock<MOrder> _newOrdersBuffer = new BufferBlock<MOrder>();
        private readonly List<MOrder> _orders = new List<MOrder>();

        private readonly MarketDataService _marketDataService;
        private readonly DealEndingService _dealEndingService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataHolder _marketDataHolder;

        /// <summary>
        /// DI constructor
        /// </summary>
        /// <param name="marketDataService"></param>
        /// <param name="dealEndingService"></param>
        /// <param name="logger"></param>
        /// <param name="ordersMatcher"></param>
        /// <param name="serviceScopeFactory"></param>
        public MatchingPool(
            MarketDataService marketDataService,
            DealEndingService dealEndingService,
            ILogger<MatchingPool> logger,
            OrdersMatcher ordersMatcher,
            IServiceScopeFactory serviceScopeFactory, MarketDataHolder marketDataHolder)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _marketDataHolder = marketDataHolder;
            _dealEndingService = dealEndingService;
            _logger = logger;
            _ordersMatcher = ordersMatcher;
            _marketDataService = marketDataService;

            LoadOrders();
        }

        private MBid GetMBid(Bid bid)
        {
            return new MBid()
            {
                Id = bid.Id,
                UserId = bid.UserId,
                Volume = bid.Volume,
                Fulfilled = bid.Fulfilled,
                Price = bid.Price,
                Created = bid.OrderDateUtc,
                CurrencyPairId = bid.CurrencyPairId,
                ExchangeId = bid.ExchangeId,
                FromInnerTradingBot = bid.FromInnerTradingBot,
                Status = MStatus.Active
            };
        }

        private MAsk GetMAsk(Ask ask)
        {
            return new MAsk()
            {
                Id = ask.Id,
                UserId = ask.UserId,
                Volume = ask.Volume,
                Fulfilled = ask.Fulfilled,
                Price = ask.Price,
                Created = ask.OrderDateUtc,
                CurrencyPairId = ask.CurrencyPairId,
                ExchangeId = ask.ExchangeId,
                FromInnerTradingBot = ask.FromInnerTradingBot,
                Status = MStatus.Active
            };
        }

        private void LoadOrders()
        {
            using (var constructorScope = _serviceScopeFactory.CreateScope())
            {
                var dbOrders = new List<MOrder>();
                var db = constructorScope.ServiceProvider.GetRequiredService<TradingDbContext>();

                List<Ask> asks = db.Asks.AsNoTracking().Where(a => a.OrderTypeCode == OrderType.Active.Code).ToList();

                _logger.LogInformation($"Loaded {asks.Count} asks from database");

                foreach (var ask in asks)
                {
                    MAsk ma = GetMAsk(ask);

                    dbOrders.Add(ma);
                }

                List<Bid> bids = db.Bids.AsNoTracking().Where(a => a.OrderTypeCode == OrderType.Active.Code).ToList();

                _logger.LogInformation($"Loaded {bids.Count} bids from database");

                foreach (var bid in bids)
                {
                    MBid mb = GetMBid(bid);

                    dbOrders.Add(mb);
                }

                foreach (var mOrder in dbOrders.OrderBy(o => o.Created))
                {
                    _newOrdersBuffer.Post(mOrder);
                }
            }
        }

        public CurrencyPairPrices GetCurrencyPairPrices(string currencyPairCode)
        {
            var orders = _orders.Where(_ => _.Status == MStatus.Active && _.CurrencyPairId == currencyPairCode && !_.FromInnerTradingBot).ToList();
            return new CurrencyPairPrices
            {
                CurrencyPair = currencyPairCode,
                BidMax = orders.Where(_ => _.CurrencyPairId == currencyPairCode && _.IsBid).Select(_ => _.Price).DefaultIfEmpty().Max(),
                AskMin = _orders.Where(_ => _.CurrencyPairId == currencyPairCode && !_.IsBid).Select(_ => _.Price).DefaultIfEmpty().Min(),
            };
        }

        private Deal GetDbDeal(MDeal mDeal)
        {
            Deal deal = new Deal
            {
                AskId = mDeal.Ask.Id,
                BidId = mDeal.Bid.Id,
                Volume = mDeal.Volume,
                DealId = Guid.NewGuid(),
                DealDateUtc = DateTime.Now,
                Price = mDeal.Price,
                FromInnerTradingBot = mDeal.FromInnerTradingBot,
            };

            mDeal.DealId = deal.DealId;

            return deal;
        }

        private List<Deal> UpdateDatabase(TradingDbContext db, MatchingResult matcher)
        {
            if (matcher.Deals.Count > 0)
            {
                _logger.LogInformation($"Created {matcher.Deals.Count} new deals");
            }

            if (matcher.ModifiedAsks.Count > 0)
            {
                _logger.LogInformation($"Updating {matcher.ModifiedAsks.Count} asks");
                var asks = matcher.ModifiedAsks.Select(ma => ma.Id).ToList();
                var dbAsks =
                    db.Asks.Where(a => asks.Contains(a.Id))
                        .ToDictionary(a => a.Id, a => a);

                for (var index = 0; index < matcher.ModifiedAsks.Count; index++)
                {
                    var ma = matcher.ModifiedAsks[index];
                    if (!dbAsks.TryGetValue(ma.Id, out var dbAsk))
                    {
                        if (ma.ExchangeId != 0 || ma.FromInnerTradingBot) // create if external or fromInnerBot and wasn't created yet
                        {
                            dbAsk = new Ask
                            {
                                Id = ma.Id,
                                Price = ma.Price,
                                Volume = ma.Volume,
                                UserId = ma.UserId,
                                ExchangeId = ma.ExchangeId,
                                CurrencyPairId = ma.CurrencyPairId,
                                FromInnerTradingBot = ma.FromInnerTradingBot,
                                OrderDateUtc = ma.Created,
                                Fulfilled = ma.Fulfilled,
                                OrderTypeCode = OrderType.Completed.Code,
                            };
                            db.Asks.Add(dbAsk);
                        }
                        else
                        {
                            throw new Exception($"Expected ask with id [{ma.Id}] not found in DB");
                        }
                    }

                    dbAsk.Fulfilled = ma.Fulfilled;
                    if (ma.Status == MStatus.Completed)
                    {
                        dbAsk.OrderTypeCode = OrderType.Completed.Code;
                    }
                }
            }

            if (matcher.ModifiedBids.Count > 0)
            {
                _logger.LogInformation($"Updating {matcher.ModifiedBids.Count} bids");
                var bids = matcher.ModifiedBids.Select(mb => mb.Id).ToList();
                var dbBids =
                    db.Bids.Where(b => bids.Contains(b.Id))
                        .ToDictionary(b => b.Id, b => b);

                for (var index = 0; index < matcher.ModifiedBids.Count; index++)
                {
                    var mb = matcher.ModifiedBids[index];
                    if (!dbBids.TryGetValue(mb.Id, out var dbBid))
                    {
                        if (mb.ExchangeId != 0 || mb.FromInnerTradingBot) // create if external or fromInnerBot and wasn't created yet
                        {
                            dbBid = new Bid
                            {
                                Id = mb.Id,
                                Price = mb.Price,
                                Volume = mb.Volume,
                                UserId = mb.UserId,
                                ExchangeId = mb.ExchangeId,
                                CurrencyPairId = mb.CurrencyPairId,
                                FromInnerTradingBot = mb.FromInnerTradingBot,
                                OrderDateUtc = mb.Created,
                                Fulfilled = mb.Fulfilled,
                                OrderTypeCode = OrderType.Completed.Code,
                            };
                            db.Bids.Add(dbBid);
                        }
                        else
                        {
                            throw new Exception($"Expected bid with id [{mb.Id}] not found in DB");
                        }
                    }

                    dbBid.Fulfilled = mb.Fulfilled;
                    if (mb.Status == MStatus.Completed)
                    {
                        dbBid.OrderTypeCode = OrderType.Completed.Code;
                    }
                }
            }

            var newDeals = matcher.Deals.Select(GetDbDeal).ToList();
            db.Deals.AddRange(newDeals);

            db.SaveChanges();
            return newDeals;
        }

        private async Task ReportData(TradingDbContext db, MatchingResult matcher)
        {
            try
            {
                var dealGuids = matcher.Deals.Select(md => md.DealId).ToList();
                await SendOrdersToMarketData();
                if (dealGuids.Count == 0) return;
                var dbDeals = db.Deals
                    .Include(d => d.Ask)
                    .Include(d => d.Bid)
                    .Where(d => dealGuids.Contains(d.DealId))
                    .ToDictionary(d => d.DealId, d => d);
                foreach (var item in matcher.Deals)
                {
                    var dbDeal = dbDeals[item.DealId];
                    var t1 = Task.Run(async () => { await SendDealToDealEnding(item); });
                    var t2 = Task.Run(async () => { await SendFeedToMarketData(item); });
                    var t3 = Task.Run(async () => { await SendDealToMarketData(dbDeal); });

                    await t1;
                    await t2;
                    await t3;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        public async Task<SaveExternalOrderResult> UpdateExternalOrder(ExternalCreatedOrder createdOrder)
        {
            var deals = new List<MDeal>();
            var modifiedAsks = new List<MAsk>();
            var modifiedBids = new List<MBid>();
            var completedOrders = new List<MOrder>();
            decimal unfulfilled = createdOrder.Amount - createdOrder.Fulfilled;

            bool updateOrder = false;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                MatchingResult result;

                MBid bid;
                MAsk ask;
                lock (_orders)
                {
                    bid = (MBid)_orders.FirstOrDefault(x => x.Id == Guid.Parse(createdOrder.TradingBidId));
                    ask = (MAsk)_orders.FirstOrDefault(x => x.Id == Guid.Parse(createdOrder.TradingAskId));
                }

                if (bid == null)
                {
                    var dbBid = await db.Bids.SingleOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingBidId));
                    bid = GetMBid(dbBid);
                    bid.Status = dbBid.OrderTypeCode == OrderType.Active.Code ? MStatus.Active : MStatus.Completed;
                }
                if (ask == null)
                {
                    var dbAsk = await db.Asks.SingleOrDefaultAsync(_ => _.Id == Guid.Parse(createdOrder.TradingAskId));
                    ask = GetMAsk(dbAsk);
                    ask.Status = dbAsk.OrderTypeCode == OrderType.Active.Code ? MStatus.Active : MStatus.Completed;
                }

                // create a separate completed order for the matched part of the imported order
                Guid newExternalOrderId = Guid.NewGuid();
                MBid newBid = null;
                MAsk newAsk = null;
                if (createdOrder.Fulfilled > 0)
                {
                    if (createdOrder.IsBid)
                    {
                        newAsk = new MAsk
                        {
                            Id = newExternalOrderId,
                            IsBid = ask.IsBid,
                            Created = ask.Created,
                            CurrencyPairId = ask.CurrencyPairId,
                            ExchangeId = ask.ExchangeId,
                            Volume = createdOrder.Fulfilled,
                            Fulfilled = createdOrder.Fulfilled,
                            Price = ask.Price,
                            Status = MStatus.Completed,
                            UserId = $"{ask.UserId}_matched"
                        };
                        modifiedAsks.Add(newAsk);
                    }
                    else
                    {
                        newBid = new MBid
                        {
                            Id = newExternalOrderId,
                            IsBid = bid.IsBid,
                            Created = bid.Created,
                            CurrencyPairId = bid.CurrencyPairId,
                            ExchangeId = bid.ExchangeId,
                            Volume = createdOrder.Fulfilled,
                            Fulfilled = createdOrder.Fulfilled,
                            Price = bid.Price,
                            Status = MStatus.Completed,
                            UserId = $"{bid.UserId}_matched"
                        };
                        modifiedBids.Add(newBid);
                    }
                }

                Guid? resultDealId = null;
                lock (_orders)
                {
                    if (createdOrder.IsBid)
                    {
                        ask.Fulfilled -= unfulfilled;
                        if (createdOrder.Fulfilled > 0)
                        {
                            var deal = new MDeal
                            {
                                Created = DateTime.Now,
                                Ask = newAsk ?? ask,
                                Bid = newBid ?? bid,
                                Price = createdOrder.Price,
                                Volume = createdOrder.Fulfilled
                            };
                            deals.Add(deal);
                        }
                        if (ask.Fulfilled == ask.Volume)
                        {
                            ask.Status = MStatus.Completed;
                            if (ask.ExchangeId == 0)
                                completedOrders.Add(ask);
                        }
                        else
                        {
                            ask.Status = MStatus.Active;
                            updateOrder = true;
                        }
                        modifiedAsks.Add(ask);
                    }
                    else
                    {
                        bid.Fulfilled -= unfulfilled;
                        if (createdOrder.Fulfilled > 0)
                        {
                            var deal = new MDeal
                            {
                                Created = DateTime.Now,
                                Ask = newAsk ?? ask,
                                Bid = newBid ?? bid,
                                Price = createdOrder.Price,
                                Volume = createdOrder.Fulfilled
                            };
                            deals.Add(deal);
                        }
                        if (bid.Fulfilled == bid.Volume)
                        {
                            bid.Status = MStatus.Completed;
                            if (bid.ExchangeId == 0)
                                completedOrders.Add(bid);
                        }
                        else
                        {
                            bid.Status = MStatus.Active;
                            updateOrder = true;
                        }
                        modifiedBids.Add(bid);
                    }

                    result = new MatchingResult(modifiedAsks, modifiedBids, deals, completedOrders);

                    var savedDeals = UpdateDatabase(db, result);
                    if (savedDeals.Count > 0)
                        resultDealId = savedDeals.First().DealId;
                    _orders.RemoveAll(o => o.Status == MStatus.Completed);
                    if (updateOrder)
                    {
                        _orders.Remove(createdOrder.IsBid ? (MOrder)ask : bid);
                        _newOrdersBuffer.Post(createdOrder.IsBid ? (MOrder)ask : bid);
                    }
                }
                await ReportData(db, result);
                return new SaveExternalOrderResult
                {
                    NewExternalOrderId = deals.Count > 0 ? newExternalOrderId.ToString() : null,
                    CreatedDealId = resultDealId?.ToString() ?? null
                };
            }
        }

        private async Task SendOrdersToMarketData()
        {
            try
            {
                List<MOrder> activeOrders;
                lock (_orders)
                {
                    //.Where(_ => _.Status == MStatus.Active)
                    activeOrders = _orders.Where(_ => _.Status == MStatus.Active).ToList();
                }
                _marketDataHolder.SendOrders(activeOrders);
                //await _marketDataService.SendOrders(activeOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending orders to marketdata");
            }
        }

        private async Task SendDealToDealEnding(MDeal deal)
        {
            try
            {
                await _dealEndingService.SendDeal(deal.DealId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending deal to brokerage");
            }
        }

        private async Task SendFeedToMarketData(MDeal deal)
        {
            try
            {
                MarketDataFeed feed = new MarketDataFeed
                {
                    Amount = deal.Price,
                    Volume = deal.Volume,
                    Date = deal.Created,
                    Currency = deal.Ask.CurrencyPairId
                };
                await _marketDataService.PutData(feed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending feed to marketdata");
            }
        }

        private async Task SendDealToMarketData(Deal deal)
        {
            try
            {
                var dealResponse = new DealResponse()
                {
                    DealId = deal.DealId,
                    Price = deal.Price,
                    Volume = deal.Volume,
                    DealDateUtc = deal.DealDateUtc,
                    CurrencyPairId = deal.Ask.CurrencyPairId,
                    AskId = deal.Ask.Id,
                    BidId = deal.Bid.Id,
                    UserAskId = deal.Ask.UserId,
                    UserBidId = deal.Bid.UserId,
                    IsBuy = deal.Bid.OrderDateUtc > deal.Ask.OrderDateUtc
                };
                await _marketDataService.SaveNewDeal(dealResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
        }

        private async Task Process(MOrder newOrder)
        {
            _logger.LogInformation("Matching process started");
            _logger.LogInformation("Detected new order");

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                MatchingResult result;

                lock (_orders) //no access to pool (for removing) while matching is performed
                {
                    DateTime start = DateTime.Now;
                    var matchingOrders = _orders.Where(o => o.CurrencyPairId == newOrder.CurrencyPairId
                        && o.FromInnerTradingBot == newOrder.FromInnerTradingBot).ToList();
                    result = _ordersMatcher.Match(matchingOrders, newOrder);
                    UpdateDatabase(db, result);
                    _orders.Add(newOrder);
                    _orders.RemoveAll(o => o.Status == MStatus.Completed);

                    DateTime end = DateTime.Now;
                    _logger.LogInformation($"Matching completed: {(end - start).TotalMilliseconds} ms ; Orders in pool: {_orders.Count};");

                    // debug, find when bids are bigger than asks
                    var biggestBid = _orders
                        .Where(o => o.CurrencyPairId == newOrder.CurrencyPairId && o.IsBid)
                        .OrderByDescending(_ => _.Price).FirstOrDefault();
                    var lowestAsk = _orders
                        .Where(o => o.CurrencyPairId == newOrder.CurrencyPairId && !o.IsBid)
                        .OrderBy(_ => _.Price).FirstOrDefault();
                    if (biggestBid != null && lowestAsk != null && biggestBid.Price > lowestAsk.Price)
                    {
                        _logger.LogError($"\n\n\n!!!!!!! bids are bigger than asks. {newOrder.CurrencyPairId}");
                        _logger.LogError($"newOrder:   {newOrder.ExchangeId},{newOrder.FromInnerTradingBot},{newOrder.IsBid}, {newOrder.Price},{newOrder.Volume},{newOrder.Created}, {newOrder.UserId},{newOrder.Id}");
                        _logger.LogError($"biggestBid: {biggestBid.ExchangeId},{biggestBid.FromInnerTradingBot},{biggestBid.IsBid}, {biggestBid.Price},{biggestBid.Volume},{biggestBid.Created}, {biggestBid.UserId},{biggestBid.Id}");
                        _logger.LogError($"lowestAsk: {lowestAsk.ExchangeId},{lowestAsk.FromInnerTradingBot},{lowestAsk.IsBid}, {lowestAsk.Price},{lowestAsk.Volume},{lowestAsk.Created}, {lowestAsk.UserId},{lowestAsk.Id}");
                    }
                    // end debug
                }
                await ReportData(db, result);
            }
        }

        public void AppendAsk(MAsk ask)
        {
            _newOrdersBuffer.Post(ask);
        }

        public async Task RemoveAsk(Guid id)
        {
            lock (_orders)
            {
                var order = _orders.FirstOrDefault(_ => _.Id == id);
                if (order != null)
                    _orders.Remove(order);
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

        public async Task RemoveOrders(int exchangeId, string currencyPairId)
        {
            lock (_orders)
            {
                _orders.RemoveAll(_ => _.ExchangeId == exchangeId && _.CurrencyPairId == currencyPairId);
            }
            await SendOrdersToMarketData();
        }

        public async Task RemoveOldInnerBotOrders()
        {
            lock (_orders)
            {
                _orders.RemoveAll(_ => _.FromInnerTradingBot && _.Created < DateTime.UtcNow.AddSeconds(-7));
            }
            await SendOrdersToMarketData();
        }

        public void AppendBid(MBid bid)
        {
            _newOrdersBuffer.Post(bid);
        }

        public async Task RemoveBid(Guid id)
        {
            lock (_orders)
            {
                var order = _orders.FirstOrDefault(_ => _.Id == id);
                if (order != null)
                    _orders.Remove(order);
            }
            await SendOrdersToMarketData();
        }

        /// <summary>
        /// only for updating orders from other exchanges
        /// </summary>
        public async Task UpdateOrder(Guid id, AddRequest changedOrder)
        {
            lock (_orders)
            {
                var order = _orders.FirstOrDefault(_ => _.Id == id);
                if (order != null)
                {
                    order.Volume = changedOrder.Amount;
                }
            }
            await SendOrdersToMarketData();
        }

        public async Task UpdateOrders(IEnumerable<AddRequestLiquidity> orders)
        {
            lock (_orders)
            {
                foreach (var order in orders)
                {
                    var o = _orders.FirstOrDefault(_ => _.Id == Guid.Parse(order.TradingOrderId));
                    if (o != null)
                    {
                        o.Volume = order.Amount;
                    }
                }
            }
            await SendOrdersToMarketData();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (await _newOrdersBuffer.OutputAvailableAsync(cancellationToken) == false)
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

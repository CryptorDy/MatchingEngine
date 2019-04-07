using Stock.Trading.Models;
using Stock.Trading.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stock.Trading.Service
{
    public class OrdersMatcher
    {
        private readonly ILiquidityImportService _liquidityImportService;

        public OrdersMatcher(ILiquidityImportService liquidityImportService)
        {
            _liquidityImportService = liquidityImportService;
        }

        private static MDeal CreateDeal(MBid bid, MAsk ask, decimal volume, decimal price)
        {
            return new MDeal
            {
                Created = DateTime.Now,
                Ask = ask,
                Bid = bid,
                Price = price,
                Volume = volume,
                FromInnerTradingBot = bid.FromInnerTradingBot
            };
        }

        public MatchingResult Match(IEnumerable<MOrder> pool, MOrder newOrder)
        {
            var deals = new List<MDeal>();
            var modifiedAsks = new List<MAsk>();
            var modifiedBids = new List<MBid>();
            var completedOrders = new List<MOrder>();

            var modified = false;
            if (newOrder is MBid)
            {
                var asks = pool.Where(o => o is MAsk && o.Price <= newOrder.Price && (newOrder.ExchangeId == 0 || o.ExchangeId == 0)
                    && newOrder.FromInnerTradingBot == o.FromInnerTradingBot)
                    .OrderBy(o => o.Price).Cast<MAsk>().ToList();
                for (int i = 0; i < asks.Count; i++)
                {
                    var ask = asks[i];
                    //if (ask.Price > newOrder.Price) //stop when asks become too expensive
                    //{
                    //    break;
                    //}

                    var newOrderRemainingAmount = newOrder.Volume - newOrder.Fulfilled;
                    var askRemainingAmount = ask.Volume - ask.Fulfilled;
                    var fulfilmentAmount = Math.Min(newOrderRemainingAmount, askRemainingAmount);
                    if (fulfilmentAmount == 0)
                    {
                        continue;
                    }

                    bool isExternal = newOrder.ExchangeId != 0 || ask.ExchangeId != 0; // from LiquidityImport
                    if (isExternal)
                    {
                        _liquidityImportService.CreateTrade(new ExternalMatchingPair { Bid = newOrder, Ask = ask });
                    }

                    modified = true;
                    newOrder.Fulfilled += fulfilmentAmount;
                    ask.Fulfilled += fulfilmentAmount;

                    if (ask.Fulfilled == ask.Volume)
                    {
                        ask.Status = MStatus.Completed;
                        if (ask.ExchangeId == 0) completedOrders.Add(ask);
                    }

                    if (!isExternal)
                    {
                        deals.Add(CreateDeal((MBid)newOrder, ask, fulfilmentAmount, ask.Price));
                    }
                    modifiedAsks.Add(ask);

                    if (newOrder.Fulfilled == newOrder.Volume)
                    {
                        newOrder.Status = MStatus.Completed;
                        if (newOrder.ExchangeId == 0) completedOrders.Add(newOrder);
                        break;                                  // if new order is completely fulfilled there's no reason to iterate further
                    }
                }

                if (modified)
                {
                    modifiedBids.Add((MBid)newOrder);
                }
            }
            else
            {
                var bids = pool.Where(o => o is MBid && o.Price >= newOrder.Price && (newOrder.ExchangeId == 0 || o.ExchangeId == 0)
                    && newOrder.FromInnerTradingBot == o.FromInnerTradingBot)
                    .OrderByDescending(o => o.Price).Cast<MBid>().ToList();
                for (int i = 0; i < bids.Count; i++)
                {
                    var bid = bids[i];
                    //if (bid.Price < newOrder.Price) //stop when bids become too cheap
                    //{
                    //    break;
                    //}

                    var newOrderRemainingAmount = newOrder.Volume - newOrder.Fulfilled;
                    var bidRemainingAmount = bid.Volume - bid.Fulfilled;

                    var fulfilmentAmount = Math.Min(newOrderRemainingAmount, bidRemainingAmount);
                    if (fulfilmentAmount == 0)
                    {
                        continue;
                    }

                    bool isExternal = newOrder.ExchangeId != 0 || bid.ExchangeId != 0; // from LiquidityImport
                    if (isExternal)
                    {
                        _liquidityImportService.CreateTrade(new ExternalMatchingPair { Bid = bid, Ask = newOrder });
                    }

                    modified = true;
                    newOrder.Fulfilled += fulfilmentAmount;
                    bid.Fulfilled += fulfilmentAmount;

                    if (bid.Fulfilled == bid.Volume)
                    {
                        bid.Status = MStatus.Completed;
                        if (bid.ExchangeId == 0) completedOrders.Add(bid);
                    }

                    if (!isExternal)
                    {
                        deals.Add(CreateDeal(bid, (MAsk)newOrder, fulfilmentAmount, bid.Price));
                    }
                    modifiedBids.Add(bid);

                    if (newOrder.Fulfilled == newOrder.Volume)
                    {
                        newOrder.Status = MStatus.Completed;
                        if (newOrder.ExchangeId == 0) completedOrders.Add(newOrder);
                        break;                              // if new order is completely fulfilled there's no reason to iterate further
                    }
                }

                if (modified)
                {
                    modifiedAsks.Add((MAsk)newOrder);
                }
            }

            if (newOrder.Fulfilled == newOrder.Volume)
            {
                newOrder.Status = MStatus.Completed;
            }

            return new MatchingResult(modifiedAsks, modifiedBids, deals, completedOrders);
        }
    }
}

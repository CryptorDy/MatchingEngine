using MatchingEngine.Models;
using Stock.Trading.Models;
using Stock.Trading.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class OrdersMatcher
    {
        private readonly ILiquidityImportService _liquidityImportService;

        public OrdersMatcher(ILiquidityImportService liquidityImportService)
        {
            _liquidityImportService = liquidityImportService;
        }

        private static Deal CreateDeal(Order bid, Order ask, decimal volume, decimal price)
        {
            return new Deal
            {
                DateCreated = DateTimeOffset.UtcNow,
                Bid = bid,
                Ask = ask,
                Price = price,
                Volume = volume,
                FromInnerTradingBot = bid.FromInnerTradingBot
            };
        }

        public MatchingEngine.Models.MatchingResult Match(IEnumerable<Order> pool, Order newOrder)
        {
            var modifiedOrders = new List<Order>();
            var newDeals = new List<Deal>();

            var orders = pool.Where(o => o.IsBid != newOrder.IsBid && o.IsActive
                && (newOrder.Exchange == Exchange.Local || o.Exchange == Exchange.Local)
                && newOrder.FromInnerTradingBot == o.FromInnerTradingBot).ToList();
            if (newOrder.IsBid)
                orders = orders.Where(o => o.Price <= newOrder.Price).OrderBy(o => o.Price).ToList();
            else
                orders = orders.Where(o => o.Price >= newOrder.Price).OrderByDescending(o => o.Price).ToList();

            foreach (var order in orders)
            {
                var bid = newOrder.IsBid ? newOrder : order;
                var ask = newOrder.IsBid ? order : newOrder;

                var fulfilmentAmount = Math.Min(newOrder.AvailableAmount, order.AvailableAmount);
                if (fulfilmentAmount == 0)
                    continue;

                bool isExternal = newOrder.Exchange != Exchange.Local || order.Exchange != Exchange.Local; // from LiquidityImport
                if (isExternal)
                {
                    _liquidityImportService.CreateTrade(bid, ask);
                    newOrder.Blocked += fulfilmentAmount;
                    order.Blocked += fulfilmentAmount;
                }
                else
                {
                    newDeals.Add(CreateDeal(bid, ask, fulfilmentAmount, order.Price));
                    newOrder.Fulfilled += fulfilmentAmount;
                    order.Fulfilled += fulfilmentAmount;
                }

                modifiedOrders.Add(order);
                if (newOrder.AvailableAmount <= 0)
                    break; // if new order is completely fulfilled/blocked, there's no reason to iterate further
            }

            if (modifiedOrders.Count > 0)
                modifiedOrders.Add(newOrder);

            return new MatchingEngine.Models.MatchingResult(modifiedOrders, newDeals);
        }
    }
}

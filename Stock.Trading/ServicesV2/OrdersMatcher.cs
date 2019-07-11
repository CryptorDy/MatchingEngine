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

        public (List<Order> modifiedOrders, List<Deal> newDeals) Match(IEnumerable<Order> pool, Order newOrder)
        {
            var modifiedOrders = new List<Order>();
            var newDeals = new List<Deal>();

            var poolOrders = pool.Where(o => o.CurrencyPairCode == newOrder.CurrencyPairCode && o.IsBid != newOrder.IsBid
                && o.IsActive && (newOrder.IsLocal || o.IsLocal) && newOrder.FromInnerTradingBot == o.FromInnerTradingBot)
                .ToList();
            if (newOrder.IsBid)
                poolOrders = poolOrders.Where(o => o.Price <= newOrder.Price).OrderBy(o => o.Price).ToList();
            else
                poolOrders = poolOrders.Where(o => o.Price >= newOrder.Price).OrderByDescending(o => o.Price).ToList();

            foreach (var poolOrder in poolOrders)
            {
                var bid = newOrder.IsBid ? newOrder : poolOrder;
                var ask = newOrder.IsBid ? poolOrder : newOrder;

                bool isExternalTrade = !newOrder.IsLocal || !poolOrder.IsLocal;
                if (isExternalTrade)
                {
                    if (bid.Blocked != 0 || ask.Blocked != 0)
                        Console.WriteLine($"Incorrect blocked state: {bid}, {ask}");
                    _liquidityImportService.CreateTrade(bid, ask);
                    // liquidity will try to fill all amount of local order
                    newOrder.Blocked = newOrder.AvailableAmount;
                    poolOrder.Blocked = poolOrder.AvailableAmount;
                }
                else
                {
                    decimal fulfilmentAmount = Math.Min(newOrder.AvailableAmount, poolOrder.AvailableAmount);
                    newDeals.Add(new Deal(bid, ask, poolOrder.Price, fulfilmentAmount));
                    newOrder.Fulfilled += fulfilmentAmount;
                    poolOrder.Fulfilled += fulfilmentAmount;
                }

                modifiedOrders.Add(poolOrder);
                if (newOrder.AvailableAmount <= 0)
                    break; // if new order is completely fulfilled/blocked, there's no reason to iterate further
            }

            if (modifiedOrders.Count > 0)
                modifiedOrders.Add(newOrder);
            return (modifiedOrders, newDeals);
        }
    }
}

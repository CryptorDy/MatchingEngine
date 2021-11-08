using MatchingEngine.Models;
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

        private bool CanBeFilled(MatchingOrder order) => order.IsActive && order.AvailableAmount > 0;

        private bool NotBothImported(MatchingOrder order1, MatchingOrder order2) => order1.IsLocal || order2.IsLocal;

        public (List<MatchingOrder> modifiedOrders, List<Deal> newDeals) Match(IEnumerable<MatchingOrder> pool, MatchingOrder newOrder)
        {
            var modifiedOrders = new List<MatchingOrder>();
            var newDeals = new List<Deal>();

            var poolOrdersQuery = pool.Where(o => o.HasSameCurrencyPair(newOrder)
                && !o.HasSameOrderbookSide(newOrder)
                && CanBeFilled(o)
                && NotBothImported(o, newOrder)
                && o.HasSameTradingBotFlag(newOrder));
            List<MatchingOrder> poolOrders;
            if (newOrder.IsBid)
            {
                poolOrders = poolOrdersQuery.Where(o => o.Price <= newOrder.Price).OrderBy(o => o.Price).ToList();
            }
            else
            {
                poolOrders = poolOrdersQuery.Where(o => o.Price >= newOrder.Price).OrderByDescending(o => o.Price).ToList();
            }

            foreach (var poolOrder in poolOrders)
            {
                var bid = newOrder.IsBid ? newOrder : poolOrder;
                var ask = newOrder.IsBid ? poolOrder : newOrder;

                bool isExternalTrade = !newOrder.IsLocal || !poolOrder.IsLocal;
                if (isExternalTrade)
                {
                    if (bid.Blocked != 0 || ask.Blocked != 0)
                    {
                        Console.WriteLine($"Incorrect blocked state: {bid}, {ask}");
                    }

                    _liquidityImportService.CreateTrade(bid, ask);
                    // liquidity will try to fill all amount of local order
                    newOrder.Blocked = newOrder.AvailableAmount;
                    poolOrder.Blocked = poolOrder.AvailableAmount;
                    newOrder.LiquidityBlocksCount++;
                    poolOrder.LiquidityBlocksCount++;
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

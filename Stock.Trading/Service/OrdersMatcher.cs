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

        private bool AreOpposite(Order order1, Order order2) => order1.IsBid != order2.IsBid;

        private bool CanBeFilled(Order order) => order.IsActive && order.AvailableAmount > 0;

        private bool NotBothImported(Order order1, Order order2) => order1.IsLocal || order2.IsLocal;

        private bool HaveSameTradingBotFlag(Order order1, Order order2)
        {
            bool isOrder1FromDealsBot = order1.ClientType == ClientType.DealsBot;
            bool isOrder2FromDealsBot = order2.ClientType == ClientType.DealsBot;
            return isOrder1FromDealsBot == isOrder2FromDealsBot;
        }

        public (List<Order> modifiedOrders, List<Deal> newDeals) Match(IEnumerable<Order> pool, Order newOrder)
        {
            var modifiedOrders = new List<Order>();
            var newDeals = new List<Deal>();

            var poolOrdersQuery = pool.Where(o => o.CurrencyPairCode == newOrder.CurrencyPairCode
                && AreOpposite(o, newOrder) && CanBeFilled(o)
                && NotBothImported(o, newOrder)
                && HaveSameTradingBotFlag(o, newOrder));
            List<Order> poolOrders;
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
                    newOrder.SetIsActive();
                    poolOrder.Fulfilled += fulfilmentAmount;
                    poolOrder.SetIsActive();
                }

                modifiedOrders.Add(poolOrder);
                if (newOrder.AvailableAmount <= 0)
                {
                    break; // if new order is completely fulfilled/blocked, there's no reason to iterate further
                }
            }

            if (modifiedOrders.Count > 0)
            {
                modifiedOrders.Add(newOrder);
            }

            return (modifiedOrders, newDeals);
        }
    }
}

using AutoMapper;
using MatchingEngine.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class OrdersMatcher
    {
        private readonly ILiquidityImportService _liquidityImportService;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        public OrdersMatcher(ILiquidityImportService liquidityImportService,
            IMapper mapper,
            ILogger<OrdersMatcher> logger)
        {
            _liquidityImportService = liquidityImportService;
            _mapper = mapper;
            _logger = logger;
        }

        private bool CanBeFilled(MatchingOrder order) => order.IsActive && order.AvailableAmount > 0;

        private bool NotBothImported(MatchingOrder order1, MatchingOrder order2) => order1.IsLocal || order2.IsLocal;

        public (List<MatchingOrder> modifiedOrders, List<Deal> newDeals, List<MatchingExternalTrade> liquidityTrades)
            Match(IEnumerable<MatchingOrder> pool, MatchingOrder newOrder)
        {
            List<MatchingOrder> modifiedOrders = new();
            List<Deal> newDeals = new();
            List<MatchingExternalTrade> liquidityTrades = new();

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
                int maxLiquidityBlocks = 3;
                if (isExternalTrade)
                {
                    if (bid.Blocked != 0 || ask.Blocked != 0)
                        _logger.LogWarning($"Incorrect blocked state: {bid}, {ask}");

                    if (newOrder.LiquidityBlocksCount > maxLiquidityBlocks || poolOrder.LiquidityBlocksCount > maxLiquidityBlocks)
                        continue;
                    // liquidity will try to fill all amount of local order
                    var externalTrade = new MatchingExternalTrade(bid, ask, _mapper);
                    _liquidityImportService.CreateTrade(externalTrade);
                    newOrder.Blocked = newOrder.AvailableAmount;
                    poolOrder.Blocked = poolOrder.AvailableAmount;
                    newOrder.LiquidityBlocksCount++;
                    poolOrder.LiquidityBlocksCount++;

                    externalTrade.Bid = null; // prevent save of related entities
                    externalTrade.Ask = null;
                    liquidityTrades.Add(externalTrade);
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

            return (modifiedOrders, newDeals, liquidityTrades);
        }
    }
}

using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class MarketDataSender : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly GatewayHttpClient _gatewayHttpClient;

        public MarketDataSender(ILogger<MarketDataSender> logger,
             MarketDataService marketDataService, MarketDataHolder marketDataHolder, GatewayHttpClient gatewayHttpClient)
        {
            _logger = logger;
            _marketDataService = marketDataService;
            _marketDataHolder = marketDataHolder;
            _gatewayHttpClient = gatewayHttpClient;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_marketDataHolder.RefreshMarketData())
                    {
                        var orders = _marketDataHolder.GetOrders();
                        RemoveLiquidityOrderIntersections(orders);
                        await _marketDataService.SendOrders(orders);
                        _marketDataHolder.SendComplete();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Send market data error");
                }
            }
        }

        private void RemoveLiquidityOrderIntersections(List<Order> orders)
        {
            var currencyPairs = orders.Select(_ => _.CurrencyPairCode).Distinct().ToList();
            foreach(string currencyPair in currencyPairs)
            {
                decimal maxBidPrice = orders.Where(_ => _.CurrencyPairCode == currencyPair && _.IsBid)
                    .Select(_ => _.Price).DefaultIfEmpty().Max();
                if (maxBidPrice > 0)
                {
                    int deleted = orders.RemoveAll(_ => _.CurrencyPairCode == currencyPair && !_.IsBid
                        && _.ClientType == ClientType.LiquidityBot && _.Price <= maxBidPrice);
                    if (deleted > 0)
                    {
                        Console.WriteLine($"RemoveOrderbookIntersections() removed {deleted} {currencyPair} asks");
                    }
                }

                decimal minAskPrice = orders.Where(_ => _.CurrencyPairCode == currencyPair && !_.IsBid)
                    .Select(_ => _.Price).DefaultIfEmpty().Min();
                if (minAskPrice > 0)
                {
                    int deleted = orders.RemoveAll(_ => _.CurrencyPairCode == currencyPair && _.IsBid
                        && _.ClientType == ClientType.LiquidityBot && _.Price >= minAskPrice);
                    if (deleted > 0)
                    {
                        Console.WriteLine($"RemoveOrderbookIntersections() removed {deleted} {currencyPair} bids");
                    }
                }
            }
        }
    }
}

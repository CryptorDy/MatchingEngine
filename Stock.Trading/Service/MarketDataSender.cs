using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly ILogger _logger;

        public MarketDataSender(IServiceScopeFactory serviceScopeFactory,
            GatewayHttpClient gatewayHttpClient,
            MarketDataService marketDataService,
            MarketDataHolder marketDataHolder,
            ILogger<MarketDataSender> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _gatewayHttpClient = gatewayHttpClient;
            _marketDataService = marketDataService;
            _marketDataHolder = marketDataHolder;
            _logger = logger;
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
                        await _marketDataService.SendActiveOrders(orders);
                        await SendDbOrderEvents();
                        _marketDataHolder.SendComplete();}
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

        private async Task SendDbOrderEvents()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                var newEvents = await context.OrderEvents.Where(_ => !_.IsSavedInMarketData).ToListAsync();
                if (newEvents.Count == 0)
                {
                    return;
                }

                // select last unsent event for each order to create/update order in MarketData
                var eventsForSend = newEvents.OrderByDescending(_ => _.EventDate)
                    .GroupBy(_ => _.Id).Select(g => g.First()).ToList();
                bool isSuccess = await _marketDataService.SaveOrdersFromEvents(eventsForSend);
                if (!isSuccess)
                {
                    return;
                }

                foreach (var newEvent in newEvents)
                {
                    newEvent.IsSavedInMarketData = true;
                }
                await context.SaveChangesAsync();
            }
        }

        private void RemoveLiquidityOrderIntersections(List<Order> orders)
        {
            var currencyPairs = orders.Select(_ => _.CurrencyPairCode).Distinct().ToList();
            foreach (string currencyPair in currencyPairs)
            {
                decimal maxBidPrice = orders.Where(_ => _.CurrencyPairCode == currencyPair && _.IsBid && _.ClientType == ClientType.LiquidityBot)
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

                decimal minAskPrice = orders.Where(_ => _.CurrencyPairCode == currencyPair && !_.IsBid && _.ClientType == ClientType.LiquidityBot)
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

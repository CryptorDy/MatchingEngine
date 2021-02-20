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
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly ILogger _logger;

        public MarketDataSender(IServiceScopeFactory serviceScopeFactory,
            MarketDataService marketDataService,
            MarketDataHolder marketDataHolder,
            ILogger<MarketDataSender> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
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
                    if (_marketDataHolder.NeedsUpdate())
                    {
                        var pairs = _marketDataHolder.DequeueAllPairsForSend();
                        await Task.WhenAll(pairs.Select(async pairCode =>
                        {
                            var orders = _marketDataHolder.GetOrders(pairCode);
                            RemoveLiquidityOrderIntersections(orders);
                            await _marketDataService.SendActiveOrders(pairCode, orders);
                        }));
                        await SendDbOrderEvents();
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken).ContinueWith(task => { });
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

                const int batchSize = 1000;
                while (true)
                {
                    var newEvents = await context.OrderEvents.Where(_ => !_.IsSavedInMarketData)
                        .Take(batchSize).ToListAsync();

                    // select last unsent event for each order to create/update order in MarketData
                    var eventsForSend = newEvents
                        .GroupBy(_ => _.Id).Select(g => g.OrderByDescending(_ => _.EventDate).First()).ToList();
                    bool isSuccess = await _marketDataService.SaveOrdersFromEvents(eventsForSend);
                    if (!isSuccess)
                        break;

                    foreach (var newEvent in newEvents)
                        newEvent.IsSavedInMarketData = true;
                    await context.SaveChangesAsync();

                    if (newEvents.Count < batchSize)
                        break;
                }
            }
        }

        private void RemoveLiquidityOrderIntersections(List<Order> orders)
        {
            if (!orders.Any())
                return;
            var pairCode = orders.First().CurrencyPairCode;
            decimal maxBidPrice = orders.Where(_ => _.IsBid && _.ClientType == ClientType.LiquidityBot)
                .Select(_ => _.Price).DefaultIfEmpty().Max();
            if (maxBidPrice > 0)
            {
                int deleted = orders.RemoveAll(_ => !_.IsBid && _.ClientType == ClientType.LiquidityBot && _.Price <= maxBidPrice);
                if (deleted > 0)
                    _logger.LogInformation($"RemoveOrderbookIntersections() removed {deleted} {pairCode} asks");
            }

            decimal minAskPrice = orders.Where(_ => !_.IsBid && _.ClientType == ClientType.LiquidityBot)
                .Select(_ => _.Price).DefaultIfEmpty().Min();
            if (minAskPrice > 0)
            {
                int deleted = orders.RemoveAll(_ => _.IsBid && _.ClientType == ClientType.LiquidityBot && _.Price >= minAskPrice);
                if (deleted > 0)
                    _logger.LogInformation($"RemoveOrderbookIntersections() removed {deleted} {pairCode} bids");
            }
        }
    }
}

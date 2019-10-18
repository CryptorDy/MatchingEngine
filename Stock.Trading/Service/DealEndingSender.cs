using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class DealEndingSender : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;

        private const int _batchSize = 50;

        public DealEndingSender(
            IServiceScopeFactory scopeFactory,
            ILogger<DealEndingSender> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(10 * 1000);
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                        var dealEndingService = scope.ServiceProvider.GetRequiredService<IDealEndingService>();

                        var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                            .Where(_ => !_.IsProcessed && !_.FromInnerTradingBot)
                            .Take(_batchSize).ToListAsync();

                        foreach (var deal in unprocessedDeals)
                        {
                            try
                            {
                                await dealEndingService.SendDeal(deal);
                                deal.IsProcessed = true;
                                await context.SaveChangesAsync();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                }
            }
        }
    }
}

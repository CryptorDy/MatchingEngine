using MatchingEngine.Data;
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

        private const int _batchSize = 100;

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
                await SendDeals();
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }

        public async Task SendDeals()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                    var dealEndingService = scope.ServiceProvider.GetRequiredService<IDealEndingService>();

                    var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                        .Where(_ => !_.IsSentToDealEnding && !_.FromInnerTradingBot)
                        .OrderBy(_ => _.DateCreated)
                        .Take(_batchSize)
                        .ToListAsync();
                    if (unprocessedDeals.Count == 0)
                        return;
                    _logger.LogInformation($"SendDeals() unprocessed:{unprocessedDeals.Count}");

                    int errorsCount = 0;
                    foreach (var deal in unprocessedDeals)
                    {
                        try
                        {
                            await context.LogDealExists(deal.DealId, "SendDeals before");
                            await dealEndingService.SendDeal(deal);
                            deal.IsSentToDealEnding = true;
                            await context.SaveChangesAsync();
                            await context.LogDealExists(deal.DealId, "SendDeals after");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                            errorsCount++;
                        }
                    }
                    _logger.LogInformation($"SendDeals() end. processed:{unprocessedDeals.Count}, with errors: {errorsCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
        }
    }
}

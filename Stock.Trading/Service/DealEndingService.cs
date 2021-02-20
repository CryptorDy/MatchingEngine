using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public interface IDealEndingService
    {
        Task SendDeal(Deal deal);

        Task SendDeals();
    }

    public class DealEndingService : IDealEndingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        private const int _batchSize = 100;

        public DealEndingService(
            IServiceScopeFactory serviceScopeFactory,
            GatewayHttpClient gatewayHttpClient,
            ILogger<DealEndingService> logger)
        {
            _scopeFactory = serviceScopeFactory;
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task SendDeal(Deal deal)
        {
            await _gatewayHttpClient.PostJsonAsync($"dealending/deal", deal);
        }

        public async Task SendDeals()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                    var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                        .Where(_ => !_.IsSentToDealEnding && !_.FromInnerTradingBot)
                        .OrderByDescending(_ => _.DateCreated)
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
                            await SendDeal(deal);
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

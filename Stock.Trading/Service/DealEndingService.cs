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
        private bool _isSending = false;

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
            if (_isSending)
                return;
            try
            {
                _isSending = true;
                while (true)
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
                            break;
                        _logger.LogInformation($"SendDeals() unprocessed:{unprocessedDeals.Count}");

                        int errorsCount = 0;
                        foreach (var deal in unprocessedDeals)
                        {
                            try
                            {
                                await SendDeal(deal);
                                deal.IsSentToDealEnding = true;
                                await context.SaveChangesAsync();
                                _logger.LogInformation($"SendDeals() sent: {deal.DealId}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                                errorsCount++;
                            }
                        }
                        _logger.LogInformation($"SendDeals() end. processed:{unprocessedDeals.Count}, with errors: {errorsCount}");
                    }
                    await Task.Delay(200);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
            _isSending = false;
        }
    }
}

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
    public class MarketDataDealsSender : BackgroundService
    {
        private readonly MarketDataService _marketDataService;
        private readonly ILogger _logger;

        public MarketDataDealsSender(
            MarketDataService marketDataService,
            ILogger<MarketDataDealsSender> logger)
        {
            _marketDataService = marketDataService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            //await Task.Delay(TimeSpan.FromHours(2), cancellationToken); // initial delay after service start

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _marketDataService.SendDealsFromDate(DateTimeOffset.UtcNow.AddDays(-2));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }
                await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
            }
        }
    }
}

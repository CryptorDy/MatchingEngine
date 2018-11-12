using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stock.Trading.Models.LiquidityImport;
using Stock.Trading.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stock.Trading.Service
{
    public class InnerBotExpireWatcher : Services.BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private MatchingPool _matchingPool;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        public InnerBotExpireWatcher(
            IServiceScopeFactory scopeFactory, 
            IOptions<AppSettings> settings,
            ILogger<LiquidityExpireWatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings;
            _logger = logger;
        }

        public void SetMatchingPool(MatchingPool matchingPool)
        {
            _matchingPool = matchingPool;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _matchingPool.RemoveOldInnerBotOrders();
                await Task.Delay(1 * 1000);
            }
        }
    }
}

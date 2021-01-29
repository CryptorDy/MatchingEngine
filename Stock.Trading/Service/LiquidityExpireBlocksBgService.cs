using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class LiquidityExpireBlocksBgService : BackgroundService
    {
        private readonly LiquidityExpireBlocksHandler _liquidityExpireBlocksHandler;
        private readonly ILogger _logger;

        public LiquidityExpireBlocksBgService(LiquidityExpireBlocksHandler liquidityExpireBlocksHandler,
            ILogger<LiquidityExpireBlocksBgService> logger)
        {
            _liquidityExpireBlocksHandler = liquidityExpireBlocksHandler;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1 * 60 * 1000);

                try
                {
                    await _liquidityExpireBlocksHandler.CheckBlockings();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class InnerBotExpireWatcher : BackgroundService
    {
        private MatchingPoolsHandler _matchingPoolsHandler;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        public InnerBotExpireWatcher(
            IOptions<AppSettings> settings,
            ILogger<LiquidityExpireWatcher> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public void SetMatchingPoolsHandler(MatchingPoolsHandler matchingPoolsHandler)
        {
            _matchingPoolsHandler = matchingPoolsHandler;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_matchingPoolsHandler != null)
                    {
                        foreach (var pool in _matchingPoolsHandler.GetExistingPools())
                            pool.RemoveOldInnerBotOrders();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }
}

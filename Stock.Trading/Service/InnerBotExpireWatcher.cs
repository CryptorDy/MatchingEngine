using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class InnerBotExpireWatcher : BackgroundService
    {
        private MatchingPool _matchingPool;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        public InnerBotExpireWatcher(
            IOptions<AppSettings> settings,
            ILogger<LiquidityExpireWatcher> logger)
        {
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
                if (_matchingPool != null)
                {
                    await _matchingPool.RemoveOldInnerBotOrders();
                }
                await Task.Delay(1 * 1000);
            }
        }
    }
}

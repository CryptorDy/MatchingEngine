using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
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

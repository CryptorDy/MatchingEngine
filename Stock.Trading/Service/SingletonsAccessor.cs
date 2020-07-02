using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class SingletonsAccessor
    {
        private readonly List<IHostedService> _hostedServices;

        public SingletonsAccessor(IEnumerable<IHostedService> hostedServices)
        {
            _hostedServices = hostedServices.ToList();
        }

        public MatchingPool MatchingPool => _hostedServices.FirstOrDefault(s => s is MatchingPool) as MatchingPool;
        public MarketDataSender MarketDataSender => _hostedServices.FirstOrDefault(s => s is MarketDataSender) as MarketDataSender;
        public DealEndingSender DealEndingSender => _hostedServices.FirstOrDefault(s => s is DealEndingSender) as DealEndingSender;
        public LiquidityExpireWatcher LiquidityExpireWatcher => _hostedServices.FirstOrDefault(s => s is LiquidityExpireWatcher) as LiquidityExpireWatcher;
        public LiquidityExpireBlocksWatcher LiquidityExpireBlocksWatcher => _hostedServices.FirstOrDefault(s => s is LiquidityExpireBlocksWatcher) as LiquidityExpireBlocksWatcher;
        public InnerBotExpireWatcher InnerBotExpireWatcher => _hostedServices.FirstOrDefault(s => s is InnerBotExpireWatcher) as InnerBotExpireWatcher;
    }
}

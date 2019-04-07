using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;

namespace Stock.Trading.Service
{
    public class LiquidityExpireWatcherAccessor
    {
        private readonly List<IHostedService> _hostedServices;

        public LiquidityExpireWatcherAccessor(IEnumerable<IHostedService> hostedServices)
        {
            _hostedServices = hostedServices.ToList();
        }

        public LiquidityExpireWatcher LiquidityExpireWatcher => _hostedServices.FirstOrDefault(s => s is LiquidityExpireWatcher) as LiquidityExpireWatcher;
        public InnerBotExpireWatcher InnerBotExpireWatcher => _hostedServices.FirstOrDefault(s => s is InnerBotExpireWatcher) as InnerBotExpireWatcher;
    }
}

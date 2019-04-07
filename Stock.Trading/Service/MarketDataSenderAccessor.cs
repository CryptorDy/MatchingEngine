using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;

namespace Stock.Trading.Service
{
    public class MarketDataSenderAccessor
    {
        private readonly List<IHostedService> _hostedServices;

        public MarketDataSenderAccessor(IEnumerable<IHostedService> hostedServices)
        {
            _hostedServices = hostedServices.ToList();
        }

        public MarketDataSender MarketDataSender => _hostedServices.FirstOrDefault(s => s is MarketDataSender) as MarketDataSender;
    }
}

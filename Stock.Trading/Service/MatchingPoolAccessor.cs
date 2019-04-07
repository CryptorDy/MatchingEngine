using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;

namespace Stock.Trading.Service
{
    public class MatchingPoolAccessor
    {
        private readonly List<IHostedService> _hostedServices;

        public MatchingPoolAccessor(IEnumerable<IHostedService> hostedServices)
        {
            _hostedServices = hostedServices.ToList();
        }

        public MatchingPool MatchingPool => _hostedServices.FirstOrDefault(s => s is MatchingPool) as MatchingPool;
    }
}

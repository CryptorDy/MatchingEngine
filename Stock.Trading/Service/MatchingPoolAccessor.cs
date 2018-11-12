using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

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

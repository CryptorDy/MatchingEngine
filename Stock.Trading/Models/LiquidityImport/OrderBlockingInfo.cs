using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models.LiquidityImport
{
    public class OrderBlockingInfo
    {
        public Guid OrderId { get; set; }

        public string CurrencyPairCode { get; set; }

        public DateTimeOffset DateBlocked { get; set; }
    }
}

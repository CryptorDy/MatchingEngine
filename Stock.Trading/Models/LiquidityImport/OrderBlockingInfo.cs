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

        public DateTime DateBlocked { get; set; }
    }
}

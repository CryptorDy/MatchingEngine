using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ExternalMatchingPair
    {
        public Order Bid { get; set; }
        public Order Ask { get; set; }
    }
}

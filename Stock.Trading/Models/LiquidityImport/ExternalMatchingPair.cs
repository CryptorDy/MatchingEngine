using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Models.LiquidityImport
{
    public class ExternalMatchingPair
    {
        public MOrder Bid { get; set; }
        public MOrder Ask { get; set; }
    }
}

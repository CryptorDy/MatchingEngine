using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models.LiquidityImport
{
    public class LiquidityBlock
    {
        public Guid OrderId { get; set; }
        public bool IsBid { get; set; }

        public Guid BlockId { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Models.LiquidityImport
{
    public class SaveExternalOrderResult
    {
        public string NewExternalOrderId { get; set; }
        public string CreatedDealId { get; set; }
    }
}

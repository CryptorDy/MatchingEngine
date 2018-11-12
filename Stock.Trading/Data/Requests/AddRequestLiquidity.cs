using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Requests
{
    /// <summary>
    /// Model for saving orders from LiquidityImport
    /// </summary>
    public class AddRequestLiquidity : AddRequest
    {
        public bool IsBid { get; set; }

        public string TradingOrderId { get; set; }
    }
}

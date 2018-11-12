using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Models.InnerTradingBot
{
    public class CurrencyPairPrices
    {
        public string CurrencyPair { get; set; }
        public decimal BidMax { get; set; }
        public decimal AskMin { get; set; }
    }
}

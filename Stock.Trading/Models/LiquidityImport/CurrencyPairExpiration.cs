using System;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Models.LiquidityImport
{
    public class CurrencyPairExpiration
    {
        public Exchange Exchange { get; set; }

        public string CurrencyPairCode { get; set; }

        public DateTime ExpirationDate { get; set; }
    }
}

using System;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ExternalCreatedOrder
    {
        public bool IsBid { get; set; }

        public decimal Amount { get; set; }

        [Obsolete]
        public decimal Price { get; set; }

        public string ExchangeOrderId { get; set; }
        public string TradingBidId { get; set; }
        public string TradingAskId { get; set; }

        public Exchange Exchange { get; set; }

        public string CurrencyPairCode { get; set; }

        public decimal Fulfilled { get; set; }
    }
}

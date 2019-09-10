namespace MatchingEngine.Models.LiquidityImport
{
    public class ExternalCreatedOrder
    {
        public bool IsBid { get; set; }

        public decimal Amount { get; set; }

        /// <summary>
        /// For finishing trade in MatchingEngine
        /// </summary>
        public decimal MatchingEngineDealPrice { get; set; }

        public string ExchangeOrderId { get; set; }
        public string TradingBidId { get; set; }
        public string TradingAskId { get; set; }

        public Exchange Exchange { get; set; }

        public string CurrencyPairCode { get; set; }

        public decimal Fulfilled { get; set; }
    }
}

using System;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ExternalTrade
    {
        public Guid Id { get; set; }

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

        public override string ToString() => $"{nameof(ExternalTrade)}({Exchange} {(IsBid ? "bid" : "ask")} " +
            $"filled {Fulfilled}/{Amount} {CurrencyPairCode}, " +
            $"{(string.IsNullOrEmpty(ExchangeOrderId) ? "" : $"ExchangeOrder:{ExchangeOrderId}, ")}" +
            $"matchingBid:{TradingBidId}, matchingAsk:{TradingAskId})";
    }
}

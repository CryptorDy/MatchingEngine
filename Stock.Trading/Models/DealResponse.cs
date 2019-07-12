using System;

namespace MatchingEngine.Models
{
    public class DealResponse
    {
        /// <summary>
        /// Deal guid id
        /// </summary>
        public Guid DealId { get; set; }

        /// <summary>
        /// Date time deal created
        /// </summary>
        public DateTime DealDateUtc { get; set; }

        /// <summary>
        /// Deal volume
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Deal price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Ask guid id
        /// </summary>
        public Guid AskId { get; set; }

        /// <summary>
        /// Bid guid id
        /// </summary>
        public Guid BidId { get; set; }

        /// <summary>
        /// Currency pair id
        /// </summary>
        public string CurrencyPairId { get; set; }

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        public bool FromInnerTradingBot { get; set; } = false;

        /// <summary>
        /// User created ask id
        /// </summary>
        public string UserAskId { get; set; }

        /// <summary>
        /// User created bid id
        /// </summary>
        public string UserBidId { get; set; }

        /// <summary>
        /// Buy or Sell type
        /// </summary>
        public bool IsBuy { get; set; }
    }
}

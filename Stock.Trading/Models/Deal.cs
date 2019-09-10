using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchingEngine.Models
{
    public class Deal
    {
        public Deal()
        {
        }

        public Deal(Order order1, Order order2, decimal price, decimal volume)
        {
            var (bid, ask) = order1.IsBid ? (order1, order2) : (order2, order1);
            if (!bid.IsBid)
                throw new Exception($"No bids passed to Deal(): {order1}, {order2}");
            if (ask.IsBid)
                throw new Exception($"No asks passed to Deal(): {order1}, {order2}");

            DateCreated = DateTimeOffset.UtcNow;
            BidId = bid.Id;
            AskId = ask.Id;
            Price = price;
            Volume = volume;
            FromInnerTradingBot = bid.FromInnerTradingBot;
        }

        /// <summary>
        /// Deal guid id
        /// </summary>
        [Key]
        public Guid DealId { get; set; }

        /// <summary>
        /// Date time deal created
        /// </summary>
        public DateTimeOffset DateCreated { get; set; }

        /// <summary>
        /// Deal volume
        /// </summary>
        [Required]
        public decimal Volume { get; set; }

        /// <summary>
        /// Deal price
        /// </summary>
        [Required]
        public decimal Price { get; set; }

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        [Required]
        public bool FromInnerTradingBot { get; set; } = false;

        /// <summary>
        /// Ask id
        /// </summary>
        public Guid AskId { get; set; }

        /// <summary>
        /// Ask
        /// </summary>
        [Required]
        [ForeignKey("AskId")]
        public Ask Ask { get; set; }

        /// <summary>
        /// Bid id
        /// </summary>
        public Guid BidId { get; set; }

        /// <summary>
        /// Bid
        /// </summary>
        [Required]
        [ForeignKey("BidId")]
        public Bid Bid { get; set; }

        public DealResponse GetDealResponse()
        {
            return new DealResponse()
            {
                DealId = DealId,
                Price = Price,
                Volume = Volume,
                DealDateUtc = DateCreated.DateTime,
                CurrencyPairId = Ask.CurrencyPairCode,
                BidId = Bid.Id,
                AskId = Ask.Id,
                UserBidId = Bid.UserId,
                UserAskId = Ask.UserId,
                IsBuy = Bid.DateCreated > Ask.DateCreated,
                FromInnerTradingBot = FromInnerTradingBot
            };
        }
    }
}

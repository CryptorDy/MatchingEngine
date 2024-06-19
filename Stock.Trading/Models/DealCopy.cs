using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Models
{
    public class DealCopy
    {
        public DealCopy() { }

        public DealCopy(Deal deal)
        {
            DealId = deal.DealId;
            DateCreated = deal.DateCreated;
            Volume = deal.Volume;
            Price = deal.Price;
            BidId = deal.BidId;
            AskId = deal.AskId;
            FromInnerTradingBot = deal.FromInnerTradingBot;
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
        /// Bid id
        /// </summary>
        public Guid BidId { get; set; }

        public override string ToString() => $"{nameof(DealCopy)}({DealId}, {DateCreated}, Volume:{Volume}," +
            $"\n bid:{BidId}, ask:{AskId})";
    }
}

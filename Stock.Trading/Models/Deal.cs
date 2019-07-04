using Stock.Trading.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchingEngine.Models
{
    public class Deal
    {
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
        public Order Ask { get; set; }

        /// <summary>
        /// Bid id
        /// </summary>
        public Guid BidId { get; set; }

        /// <summary>
        /// Bid
        /// </summary>
        [Required]
        [ForeignKey("BidId")]
        public Order Bid { get; set; }
    }
}

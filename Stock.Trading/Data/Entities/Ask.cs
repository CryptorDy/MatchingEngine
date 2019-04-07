using Stock.Trading.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Stock.Trading.Entities
{
    public class Ask
    {
        /// <summary>
        /// Order id
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Volume of selected currency
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Selected currency price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Currency pair
        /// </summary>
        [Required]
        public string CurrencyPairId { get; set; }

        /// <summary>
        /// Date time order created
        /// </summary>
        public DateTime OrderDateUtc { get; set; }

        /// <summary>
        /// User created order
        /// </summary>
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// Order type
        /// </summary>
        [Required]
        public string OrderTypeCode { get; set; }

        public virtual OrderType OrderType { get; set; }

        /// <summary>
        /// Original order exchange (0 - our exchange)
        /// </summary>
        public int ExchangeId { get; set; } = 0;

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        [Required]
        public bool FromInnerTradingBot { get; set; } = false;

        /// <summary>
        /// Executed amount
        /// </summary>
        public decimal Fulfilled { get; set; }

        public virtual List<Deal> DealList { get; set; }
    }
}

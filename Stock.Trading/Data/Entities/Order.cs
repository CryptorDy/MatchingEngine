using Stock.Trading.Data.Entities;
using Stock.Trading.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Entities
{
    public class Order
    {
        public Order(bool isBid, string currencyPairCode, decimal price, decimal amount)
        {
            IsBid = isBid;
            CurrencyPairCode = currencyPairCode;
            Price = price;
            Amount = amount;
        }

        [Key]
        public Guid Id { get; set; }

        public bool IsBid { get; set; }

        public decimal Price { get; set; }

        /// <summary>
        /// Base currency amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Executed amount
        /// </summary>
        public decimal Fulfilled { get; set; }

        /// <summary>
        /// Amount that is being processed by LiquidityImport
        /// </summary>
        public decimal Blocked { get; set; }

        [Required]
        public string CurrencyPairCode { get; set; }

        public DateTimeOffset DateCreated { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Active;

        /// <summary>
        /// Original order exchange
        /// </summary>
        public Exchange Exchange { get; set; } = Exchange.Local;

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        [Required]
        public bool FromInnerTradingBot { get; set; } = false;

        public decimal AvailableAmount => (Amount - Fulfilled - Blocked);

        public virtual List<Deal> DealList { get; set; }
    }

    public enum OrderStatus
    {
        Active = 1, Canceled, Completed
    }
}

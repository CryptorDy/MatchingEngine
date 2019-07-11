using Stock.Trading.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public class AddOrderDto
    {
        /// <summary>
        /// Busines process id,  order Id
        /// </summary>
        public Guid Id { get; set; }

        public bool IsBid { get; set; }

        public decimal Price { get; set; }

        public decimal Amount { get; set; }

        public string CurrencyPairCode { get; set; }

        public DateTimeOffset DateCreated { get; set; }

        /// <summary>
        /// User created order
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Original order exchange
        /// </summary>
        public Exchange Exchange { get; set; } = Exchange.Local;

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        public bool FromInnerTradingBot { get; set; } = false;
    }
}

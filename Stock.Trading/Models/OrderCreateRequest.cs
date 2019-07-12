using MatchingEngine.Models.LiquidityImport;
using System;

namespace MatchingEngine.Models
{
    public class OrderCreateRequest
    {
        /// <summary>
        /// Becomes order Id in matching-engine
        /// </summary>
        public string ActionId { get; set; }

        public bool IsBid { get; set; }
        public string CurrencyPairCode { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public string UserId { get; set; }
        public DateTimeOffset DateCreated { get; set; }

        // Optional fields:

        /// <summary>
        /// Original order exchange, only comes from LiquidtyImport
        /// </summary>
        public Exchange Exchange { get; set; } = Exchange.Local;

        /// <summary>
        /// Is created by inner trading bot
        /// </summary>
        public bool FromInnerTradingBot { get; set; } = false;

        public Order GetOrder()
        {
            return new Order
            {
                Id = Guid.Parse(ActionId),
                IsBid = IsBid,
                Price = Price,
                Amount = Amount,
                CurrencyPairCode = CurrencyPairCode,
                DateCreated = DateCreated,
                UserId = UserId,
                Exchange = Exchange,
                FromInnerTradingBot = FromInnerTradingBot
            };
        }
    }
}

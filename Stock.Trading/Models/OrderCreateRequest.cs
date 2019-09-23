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
        public DateTimeOffset DateCreated { get; set; }
        public ClientType ClientType { get; set; }
        public string UserId { get; set; }

        // Optional fields:

        /// <summary>
        /// Original order exchange, only comes from LiquidtyImport
        /// </summary>
        public Exchange Exchange { get; set; } = Exchange.Local;

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
                ClientType = ClientType,
                Exchange = Exchange,
            };
        }
    }
}

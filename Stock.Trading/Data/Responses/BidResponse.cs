using System;
using System.Collections.Generic;

namespace Stock.Trading.Responses
{
    public class BidResponse
    {
        public BidResponse()
        {
            DealResponseList = new List<DealResponse>();
        }

        public string Id { get; set; }

        /// <summary>
        /// Selected currency amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Remaining amount of order (minus all deals)
        /// </summary>
        public decimal RemainingAmount { get; set; }

        /// <summary>
        /// Remaining amount of reservation (minus all deals)
        /// </summary>
        public decimal RemainingReservationAmount { get; set; }

        /// <summary>
        /// Selected currency price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Date time order created
        /// </summary>
        public DateTime OrderDate { get; set; }

        /// <summary>
        /// User created order
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Order type
        /// </summary>
        public string OrderTypeId { get; set; }

        /// <summary>
        /// Currency pair
        /// </summary>
        public string CurrencyPairId { get; set; }

        public List<DealResponse> DealResponseList { get; set; }
    }
}

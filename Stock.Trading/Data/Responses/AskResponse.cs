using System;
using System.Collections.Generic;

namespace Stock.Trading.Responses
{
    public class AskResponse
    {
        public AskResponse()
        {
            DealResponseList = new List<DealResponse>();
        }

        public string Id { get; set; }

        /// <summary>
        /// Amount of selected currency
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Remaining amount of order (minus all deals)
        /// </summary>
        public decimal RemainingAmount { get; set; }

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
        /// Currency pair id
        /// </summary>
        public string CurrencyPairId { get; set; }

        public List<DealResponse> DealResponseList { get; set; }
    }
}
using MatchingEngine.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Models
{
    public class Bid : MatchingOrder { }

    public class Ask : MatchingOrder { }

    public class MatchingOrder : Order
    {
        public MatchingOrder()
        {
        }

        public MatchingOrder(bool isBid, string currencyPairCode, decimal price, decimal amount)
        {
            IsBid = isBid;
            CurrencyPairCode = currencyPairCode;
            Price = price;
            Amount = amount;
        }

        /// <summary>How many times was this order sent to other exchange</summary>
        public int LiquidityBlocksCount { get; set; }

        public virtual List<Deal> DealList { get; set; }

        public bool? IsActive2 { // temp name, will replace IsActive
            get {
                return !IsCanceled && Fulfilled < Amount;
            }
            set { }
        }

        public MatchingOrder Clone() => (MatchingOrder)MemberwiseClone();
    }
}

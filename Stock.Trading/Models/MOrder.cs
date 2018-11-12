using System;

namespace Stock.Trading.Models
{
    public class MOrder
    {
        public bool IsBid { get; set; }

        public Guid Id { get; set; }
        public string UserId { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public int ExchangeId { get; set; }
        public bool FromInnerTradingBot { get; set; }
        public decimal Fulfilled { get; set; }
        public DateTime Created { get; set; }
        public string CurrencyPairId { get; set; }
        public MStatus Status { get; set; }
    }
}
using System;

namespace Stock.Trading.Models
{
    public class MDeal
    {
        public Guid DealId { get; set; }

        public DateTime Created { get; set; }

        public decimal Volume { get; set; }

        public decimal Price { get; set; }

        public bool FromInnerTradingBot { get; set; }

        public MAsk Ask { get; set; }

        public MBid Bid { get; set; }
    }
}
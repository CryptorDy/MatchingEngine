using System;

namespace MatchingEngine.Models
{
    public class MarketDataFeed
    {
        public DateTime Date { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
}

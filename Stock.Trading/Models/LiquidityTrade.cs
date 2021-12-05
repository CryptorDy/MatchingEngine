using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public class LiquidityTrade
    {
        public LiquidityTrade()
        {
        }

        public LiquidityTrade(MatchingOrder bid, MatchingOrder ask)
        {
            Id = Guid.NewGuid();
            BidId = bid.Id;
            AskId = ask.Id;
            Bid = (Bid)bid;
            Ask = (Ask)ask;
            IsBid = bid.IsLocal;
        }

        public Guid Id { get; set; }
        public DateTimeOffset DateCreated { get; set; }
        public Guid BidId { get; set; }
        public Guid AskId { get; set; }
        public Guid? DealId { get; set; }
        public bool IsBid { get; set; }
        public Bid Bid { get; set; }
        public Ask Ask { get; set; }

        public override string ToString() => $"{nameof(LiquidityTrade)}({Id}, {(DealId.HasValue ? $"Deal: {DealId}, " : "")}" +
            $"\n Bid:{Bid.ToString() ?? BidId.ToString()},\n Ask:{Ask.ToString() ?? AskId.ToString()})";
    }
}

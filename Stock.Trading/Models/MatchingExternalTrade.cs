using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public enum MatchingExternalTradeStatus
    {
        Created = 0, FinishedFulfilled = 100, FinishedNotFulfilled = 110, FinishedError = 120, FinishedTimeout = 130,
    }

    public class MatchingExternalTrade
    {
        public MatchingExternalTrade()
        {
        }

        public MatchingExternalTrade(MatchingOrder bid, MatchingOrder ask, IMapper mapper)
        {
            Id = Guid.NewGuid();
            Status = MatchingExternalTradeStatus.Created;
            BidId = bid.Id;
            AskId = ask.Id;
            Bid = mapper.Map<Bid>(bid);
            Ask = mapper.Map<Ask>(ask);
            IsBid = bid.IsLocal;
        }

        public Guid Id { get; set; }
        public MatchingExternalTradeStatus Status { get; set; }
        public DateTimeOffset DateCreated { get; set; }
        public Guid BidId { get; set; }
        public Guid AskId { get; set; }
        public Guid? DealId { get; set; }
        public bool IsBid { get; set; }
        public Bid Bid { get; set; }
        public Ask Ask { get; set; }

        public override string ToString() => $"{nameof(MatchingExternalTrade)}({Id}, {(DealId.HasValue ? $"Deal: {DealId}, " : "")}" +
            $"\n Bid:{Bid?.ToString() ?? BidId.ToString()},\n Ask:{Ask?.ToString() ?? AskId.ToString()})";
    }
}

using System.Collections.Generic;

namespace Stock.Trading.Models
{
    public class MatchingResult
    {
        public MatchingResult(List<MAsk> modifiedAsks, List<MBid> modifiedBids, List<MDeal> deals, List<MOrder> completedOrders)
        {
            ModifiedAsks = modifiedAsks;
            ModifiedBids = modifiedBids;
            Deals = deals;
            CompletedOrders = completedOrders;
        }

        public List<MAsk> ModifiedAsks { get; }
        public List<MBid> ModifiedBids { get; }
        public List<MOrder> CompletedOrders { get; }
        public List<MDeal> Deals { get; }
    }
}

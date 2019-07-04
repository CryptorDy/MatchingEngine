using System.Collections.Generic;

namespace MatchingEngine.Models
{
    public class MatchingResult
    {
        public MatchingResult(List<Order> modifiedOrders, List<Deal> newDeals)
        {
            ModifiedOrders = modifiedOrders;
            NewDeals = newDeals;
        }

        public List<Order> ModifiedOrders { get; }
        public List<Deal> NewDeals { get; }
    }
}

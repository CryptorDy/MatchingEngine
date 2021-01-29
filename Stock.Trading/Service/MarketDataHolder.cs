using MatchingEngine.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private readonly ConcurrentDictionary<string, object> _pairsForUpdate =
            new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<Order>> _orders =
            new ConcurrentDictionary<string, ConcurrentQueue<Order>>();

        public void SetOrders(string pairCode, List<Order> orders)
        {
            _orders[pairCode] = new ConcurrentQueue<Order>(orders);
            _pairsForUpdate[pairCode] = null;
        }

        public List<Order> GetOrders(string pairCode)
        {
            return new List<Order>(_orders[pairCode]); // copy list to prevent concurrency error
        }

        public List<string> DequeueAllPairsForUpdate()
        {
            var pairs = _pairsForUpdate.Keys.ToList();
            _pairsForUpdate.Clear();
            return pairs;
        }

        public bool NeedsUpdate()
        {
            return _pairsForUpdate.Count > 0;
        }
    }
}

using MatchingEngine.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private readonly ConcurrentDictionary<string, object> _pairsForSend =
            new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<Order>> _orders =
            new ConcurrentDictionary<string, ConcurrentQueue<Order>>();

        public void SetOrders(string pairCode, ConcurrentDictionary<Guid, Order> orders)
        {
            _orders[pairCode] = new ConcurrentQueue<Order>(orders.Values);
            _pairsForSend.TryAdd(pairCode, new object());
        }

        public List<Order> GetOrders(string pairCode)
        {
            return new List<Order>(_orders[pairCode]); // copy list to prevent concurrency error
        }

        public List<string> DequeueAllPairsForSend()
        {
            var pairs = _pairsForSend.Keys.ToList();
            foreach (string pair in pairs)
                _pairsForSend.TryRemove(pair, out _);
            return pairs;
        }

        public bool NeedsUpdate()
        {
            return _pairsForSend.Count > 0;
        }
    }
}

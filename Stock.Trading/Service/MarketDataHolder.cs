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
        private readonly ConcurrentDictionary<string, ConcurrentQueue<MatchingOrder>> _orders =
            new ConcurrentDictionary<string, ConcurrentQueue<MatchingOrder>>();

        public void SetOrders(string pairCode, ConcurrentDictionary<Guid, MatchingOrder> orders)
        {
            _orders[pairCode] = new ConcurrentQueue<MatchingOrder>(orders.Values);
            _pairsForSend.TryAdd(pairCode, new object());
        }

        public List<MatchingOrder> GetOrders(string pairCode)
        {
            return new List<MatchingOrder>(_orders[pairCode]); // copy list to prevent concurrency error
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

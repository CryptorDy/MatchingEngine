using MatchingEngine.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private ConcurrentQueue<bool> _updateMarketData;
        private ConcurrentQueue<Order> _orders;

        public void SendOrders(List<Order> orders)
        {
            _orders = new ConcurrentQueue<Order>(orders);
            _updateMarketData.Enqueue(true);
        }

        public List<Order> GetOrders()
        {
            return new List<Order>(_orders); // copy list to prevent concurrency error
        }

        public void SendComplete()
        {
            while (_updateMarketData.TryDequeue(out _)) { } // Dequeue all
        }

        public bool RefreshMarketData()
        {
            return _updateMarketData.Count > 0;
        }
    }
}

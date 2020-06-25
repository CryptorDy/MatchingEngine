using MatchingEngine.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private ConcurrentQueue<bool> _updateMarketData = new ConcurrentQueue<bool>();
        private ConcurrentQueue<Order> _orders = new ConcurrentQueue<Order>();

        public void SendOrders(List<Order> orders)
        {
            _orders = new ConcurrentQueue<Order>(orders);
            _updateMarketData.Enqueue(true);
        }

        public List<Order> GetOrders()
        {
            return new List<Order>(_orders); // copy list to prevent concurrency error
        }

        public void ClearFlags()
        {
            while (_updateMarketData.TryDequeue(out _)) { } // Dequeue all
        }

        public bool RefreshMarketData()
        {
            return _updateMarketData.Count > 0;
        }
    }
}

using MatchingEngine.Models;
using System.Collections.Generic;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private static readonly object Locker = new object();
        private bool _updateMarketData;
        private List<Order> _orders;

        public void SendOrders(List<Order> orders)
        {
            _orders = orders;
            lock (Locker)
            {
                _updateMarketData = true;
            }
        }

        public List<Order> GetOrders()
        {
            return new List<Order>(_orders); // copy list to prevent concurrency error
        }

        public void SendComplete()
        {
            lock (Locker)
            {
                _updateMarketData = false;
            }
        }

        public bool RefreshMarketData()
        {
            lock (Locker)
            {
                return _updateMarketData;
            }
        }
    }
}

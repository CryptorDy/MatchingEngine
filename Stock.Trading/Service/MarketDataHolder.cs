using MatchingEngine.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public class MarketDataHolder
    {
        private ConcurrentQueue<bool> _updateMarketData = new ConcurrentQueue<bool>();
        private ConcurrentQueue<Order> _orders = new ConcurrentQueue<Order>();

        public void SendOrders(List<Order> orders)
        {
            var o = orders.FirstOrDefault(_ => _.CurrencyPairCode == "XSP_BTC");
            if (o != null) Console.WriteLine($"AddOrder {o.CurrencyPairCode} {o.Id} {DateTime.Now.ToString("hh:mm:ss.fff")} SendOrders");
            _orders = new ConcurrentQueue<Order>(orders);
            _updateMarketData.Enqueue(true);
            if (o != null) Console.WriteLine($"AddOrder {o.CurrencyPairCode} {o.Id} {DateTime.Now.ToString("hh:mm:ss.fff")} SendOrders end");
        }

        public List<Order> GetOrders()
        {
            return new List<Order>(_orders); // copy list to prevent concurrency error
        }

        public void SendComplete()
        {
            Console.WriteLine($"MarketDataHolder {DateTime.Now.ToString("hh:mm:ss.fff")} SendComplete()");
            while (_updateMarketData.TryDequeue(out _)) { } // Dequeue all
        }

        public bool RefreshMarketData()
        {
            return _updateMarketData.Count > 0;
        }
    }
}

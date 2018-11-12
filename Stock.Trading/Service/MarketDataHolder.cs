using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stock.Trading.Models;
using Stock.Trading.Services;

namespace Stock.Trading.Service
{
    public class MarketDataHolder
    {
        private static readonly object Locker = new object();
        private bool _updateMarketData;
        private List<MOrder> _orders;

        public void SendOrders(List<MOrder> orders)
        {
            _orders = orders;
            lock (Locker)
            {
                _updateMarketData = true;
            }
        }

        public List<MOrder> GetOrders()
        {
            return _orders;
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

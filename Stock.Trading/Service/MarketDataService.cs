using Microsoft.Extensions.Logging;
using Stock.Trading.Data;
using Stock.Trading.Entities;
using Stock.Trading.HttpClients;
using Stock.Trading.Models;
using Stock.Trading.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Service
{
    public class MarketDataService 
    {
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        public MarketDataService(ILogger<MarketDataService> logger,
            GatewayHttpClient gatewayHttpClient)
        {
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task SendOrders(List<MOrder> orders)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/orders", orders);
        }

        public async Task SaveNewDeal(DealResponse deal)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/savedeal", deal);
        }

        public async Task PutData(MarketDataFeed feed)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/putdata", feed);
        }
    }
}

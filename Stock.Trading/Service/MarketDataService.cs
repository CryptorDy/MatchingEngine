using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class MarketDataService
    {
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        public MarketDataService(GatewayHttpClient gatewayHttpClient,
            ILogger<MarketDataService> logger)
        {
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task SendOrders(List<Order> orders)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/orders", orders);
        }

        public async Task SaveNewDeal(DealResponse deal)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/savedeal", deal);
        }
    }
}

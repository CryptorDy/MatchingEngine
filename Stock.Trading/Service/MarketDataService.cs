using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.Extensions.Logging;
using System;
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

        public async Task<bool> SaveOrdersFromEvents(List<OrderEvent> events)
        {
            try
            {
                await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/order-events", events);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                return false;
            }
        }

        public async Task SendOldOrders(List<Order> orders)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/old-orders", orders);
        }

        public async Task SendActiveOrders(List<Order> orders)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders", orders);
        }

        public async Task SendDeals(List<DealResponse> deals)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/deals/old", deals);
        }

        public async Task SendNewDeal(DealResponse deal)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/savedeal", deal);
        }
    }
}

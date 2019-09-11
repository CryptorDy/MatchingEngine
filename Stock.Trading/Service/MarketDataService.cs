using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var localOrders = orders.Where(o => o.Exchange == Exchange.Local).ToList();

            Console.WriteLine($"orders sended to MD : {DateTime.UtcNow.ToLongTimeString()}" + JsonConvert.SerializeObject(localOrders,
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));

            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/orders", orders);
        }

        public async Task SaveNewDeal(DealResponse deal)
        {
            var marketDataResponse = await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/savedeal", deal);
        }
    }
}

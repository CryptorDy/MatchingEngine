using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stock.Trading.HttpClients;
using Stock.Trading.Models;

namespace Stock.Trading.Service
{
    public class DealEndingService
    {
        private readonly GatewayHttpClient _gatewayHttpClient;

        public DealEndingService(GatewayHttpClient gatewayHttpClient)
        {
            _gatewayHttpClient = gatewayHttpClient;
        }

        public async Task SendDeal(Guid dealId)
        {
            await _gatewayHttpClient.PostJsonAsync($"dealending/deal/{dealId}", null);
        }
    }
}

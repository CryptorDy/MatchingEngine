using Stock.Trading.HttpClients;
using System;
using System.Threading.Tasks;

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

using MatchingEngine.HttpClients;
using System;
using System.Threading.Tasks;

namespace MatchingEngine.Services
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

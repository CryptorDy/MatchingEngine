using MatchingEngine.HttpClients;
using MatchingEngine.Models;
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

        public async Task SendDeal(Deal deal)
        {
            await _gatewayHttpClient.PostJsonAsync($"dealending/deal", deal);
        }
    }
}

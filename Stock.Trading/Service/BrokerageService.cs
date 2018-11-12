using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stock.Trading.HttpClients;
using Stock.Trading.Models;

namespace Stock.Trading.Service
{
    public class BrokerageService
    {
        private readonly GatewayHttpClient _gatewayHttpClient;

        public BrokerageService(GatewayHttpClient gatewayHttpClient)
        {
            _gatewayHttpClient = gatewayHttpClient;
        }

        public async Task ExecuteDeal(Guid dealId)
        {
            await _gatewayHttpClient.PostJsonAsync($"brokerage/deal/{dealId}/execution", null);
        }

        public async Task CloseOrder(MOrder order)
        {
            await _gatewayHttpClient.PutAsync($"brokerage/{(order.IsBid ? "tradingaddbid" : "tradingaddask")}/{order.Id}/{order.UserId}/{order.CurrencyPairId}",
                null);
        }
    }
}

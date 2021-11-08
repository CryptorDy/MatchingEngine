using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TLabs.ExchangeSdk;

namespace MatchingEngine.Services
{
    public interface ILiquidityImportService
    {
        Task CreateTrade(MatchingOrder bid, MatchingOrder ask);

        Task RemoveOrderbook(Exchange exchange, string currencyPairCode);
    }

    public class LiquidityImportService : ILiquidityImportService
    {
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        public LiquidityImportService(ILogger<LiquidityImportService> logger,
            GatewayHttpClient gatewayHttpClient)
        {
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task CreateTrade(MatchingOrder bid, MatchingOrder ask)
        {
            try
            {
                var localOrder = bid.IsLocal ? bid : ask;
                _logger.LogInformation($"CreateTrade() start {localOrder.Id} {localOrder.CurrencyPairCode}");
                await _gatewayHttpClient.PostJsonAsync($"liquiditymain/trade/create",
                    new ExternalMatchingPair { Bid = bid, Ask = ask });
                _logger.LogInformation($"CreateTrade() end");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
            }
        }

        public async Task RemoveOrderbook(Exchange exchange, string currencyPairCode)
        {
            try
            {
                await _gatewayHttpClient.DeleteAsync($"liquiditymain/orderbook/{(int)exchange}/{currencyPairCode}");
            }
            catch (Exception e)
            {
                // error is expected because one of the reasons to remove orderbook is because liquiditymain stopped working
            }
        }
    }
}

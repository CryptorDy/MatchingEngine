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
        Task CreateTrade(MatchingExternalTrade liquidityTrade);

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

        public async Task CreateTrade(MatchingExternalTrade liquidityTrade)
        {
            try
            {
                _logger.LogInformation($"CreateTrade() start\n {liquidityTrade.Bid}\n {liquidityTrade.Ask}");
                await _gatewayHttpClient.PostJsonAsync($"liquiditymain/trade/create", liquidityTrade);
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

using Microsoft.Extensions.Logging;
using Stock.Trading.HttpClients;
using Stock.Trading.Models.LiquidityImport;
using System;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public interface ILiquidityImportService
    {
        Task CreateTrade(MatchingEngine.Models.LiquidityImport.ExternalMatchingPair matchingPair);

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

        public async Task CreateTrade(MatchingEngine.Models.LiquidityImport.ExternalMatchingPair matchingPair)
        {
            try
            {
                await _gatewayHttpClient.PostJsonAsync($"liquiditymain/trade/create", matchingPair);
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

using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public interface ILiquidityImportService
    {
        Task CreateTrade(Order bid, Order ask);

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

        public async Task CreateTrade(Order bid, Order ask)
        {
            try
            {
                await _gatewayHttpClient.PostJsonAsync($"liquiditymain/trade/create",
                    new ExternalMatchingPair { Bid = bid, Ask = ask });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                Thread.Sleep(1000); // wait to finish Match and update DB
                //_matchingPool.UpdateExternalOrder()
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

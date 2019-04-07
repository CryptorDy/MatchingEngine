using Microsoft.Extensions.Logging;
using Stock.Trading.HttpClients;
using Stock.Trading.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stock.Trading.Service
{
    public class MarketDataSender : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly GatewayHttpClient _gatewayHttpClient;

        public MarketDataSender(ILogger<MarketDataSender> logger,
             MarketDataService marketDataService, MarketDataHolder marketDataHolder, GatewayHttpClient gatewayHttpClient)
        {
            _logger = logger;
            _marketDataService = marketDataService;
            _marketDataHolder = marketDataHolder;
            _gatewayHttpClient = gatewayHttpClient;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_marketDataHolder.RefreshMarketData())
                    {
                        await _marketDataService.SendOrders(_marketDataHolder.GetOrders());
                        _marketDataHolder.SendComplete();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Send market data error");
                }
            }
        }
    }
}

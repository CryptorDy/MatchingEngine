using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stock.Trading.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stock.Trading.Service
{
    public class LiquidityExpireWatcher : Services.BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private MatchingPool _matchingPool;
        private readonly ILiquidityImportService _liquidityImportService;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        private List<CurrencyPairExpiration> CurrencyPairExpirations = new List<CurrencyPairExpiration>();

        public LiquidityExpireWatcher(
            IServiceScopeFactory scopeFactory,
            ILiquidityImportService liquidityImportService,
            IOptions<AppSettings> settings,
            ILogger<LiquidityExpireWatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _liquidityImportService = liquidityImportService;
            _settings = settings;
            _logger = logger;
        }

        public void SetMatchingPool(MatchingPool matchingPool)
        {
            _matchingPool = matchingPool;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CheckExpirationDates();
                await Task.Delay(1 * 60 * 1000);
            }
        }

        public void UpdateExpirationDate(int exchangeId, string currencyPairCode)
        {
            DateTime newExpirationDate = DateTime.UtcNow.AddMinutes(_settings.Value.ImportedOrdersExpirationMinutes);
            lock (CurrencyPairExpirations)
            {
                var expiration = CurrencyPairExpirations
                    .FirstOrDefault(_ => (int)_.Exchange == exchangeId && _.CurrencyPairCode == currencyPairCode);
                if (expiration == null)
                {
                    CurrencyPairExpirations.Add(new CurrencyPairExpiration
                    {
                        Exchange = (Exchange)exchangeId,
                        CurrencyPairCode = currencyPairCode,
                        ExpirationDate = newExpirationDate
                    });
                }
                else
                {
                    expiration.ExpirationDate = newExpirationDate;
                }
            }
        }

        private async Task CheckExpirationDates()
        {
            lock (CurrencyPairExpirations)
            {
                for (int i = 0; i < CurrencyPairExpirations.Count; i++)
                {
                    if (CurrencyPairExpirations[i].ExpirationDate < DateTime.UtcNow)
                    {
                        _logger.LogWarning($"Liquidity expired:{CurrencyPairExpirations[i].Exchange}-{CurrencyPairExpirations[i].CurrencyPairCode}");
                        _matchingPool.RemoveOrders((int)CurrencyPairExpirations[i].Exchange, CurrencyPairExpirations[i].CurrencyPairCode);
                        _liquidityImportService.RemoveOrderbook(CurrencyPairExpirations[i].Exchange, CurrencyPairExpirations[i].CurrencyPairCode);
                        CurrencyPairExpirations.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private async Task DeleteTradingOrders(int exchangeId, string currencyPairCode)
        {
            await _matchingPool.RemoveOrders(exchangeId, currencyPairCode);
        }
    }
}

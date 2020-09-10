using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class LiquidityExpireWatcher : BackgroundService
    {
        private MatchingPool _matchingPool;
        private readonly ILiquidityImportService _liquidityImportService;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        private List<CurrencyPairExpiration> CurrencyPairExpirations = new List<CurrencyPairExpiration>();

        public LiquidityExpireWatcher(
            ILiquidityImportService liquidityImportService,
            IOptions<AppSettings> settings,
            ILogger<LiquidityExpireWatcher> logger)
        {
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
                try
                {
                    await CheckExpirationDates();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }

                await Task.Delay(1 * 60 * 1000);
            }
        }

        public void UpdateExpirationDate(Exchange exchange, string currencyPairCode)
        {
            DateTime newExpirationDate = DateTime.UtcNow.AddMinutes(_settings.Value.ImportedOrderbooksExpirationMinutes);
            lock (CurrencyPairExpirations)
            {
                var expiration = CurrencyPairExpirations
                    .FirstOrDefault(_ => _.Exchange == exchange && _.CurrencyPairCode == currencyPairCode);
                if (expiration == null)
                {
                    CurrencyPairExpirations.Add(new CurrencyPairExpiration
                    {
                        Exchange = exchange,
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
            if (_matchingPool == null)
            {
                return;
            }

            List<CurrencyPairExpiration> requiredExpirations;

            lock (CurrencyPairExpirations)
            {
                requiredExpirations = CurrencyPairExpirations.Where(_ => _.ExpirationDate < DateTime.UtcNow).ToList();
            }
            foreach (var expiration in requiredExpirations)
            {
                _logger.LogWarning($"Liquidity expired:{expiration.Exchange}-{expiration.CurrencyPairCode}");
                _matchingPool.RemoveLiquidityOrderbook(expiration.Exchange, expiration.CurrencyPairCode);
                _ = _liquidityImportService.RemoveOrderbook(expiration.Exchange, expiration.CurrencyPairCode);
            }
            lock (CurrencyPairExpirations)
            {
                CurrencyPairExpirations = CurrencyPairExpirations.Except(requiredExpirations).ToList();
            }

            await _matchingPool.RemoveLiquidityOldOrders();
        }
    }
}

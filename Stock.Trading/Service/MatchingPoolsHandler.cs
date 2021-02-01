using MatchingEngine.Data;
using MatchingEngine.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class MatchingPoolsHandler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceProvider _provider;

        private readonly ConcurrentDictionary<string, MatchingPool> _matchingPools =
            new ConcurrentDictionary<string, MatchingPool>();

        public MatchingPoolsHandler(
            IServiceScopeFactory serviceScopeFactory,
            IServiceProvider provider)
        {
            _scopeFactory = serviceScopeFactory;
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            InitPools();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            const int defaultShutdownWaitPeriodSec = 5;
            var poolsCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(defaultShutdownWaitPeriodSec));
            await Task.WhenAll(_matchingPools.Values.Select(async pool =>
            {
                await pool.StopAsync(poolsCancellationSource.Token);
            }));
        }

        private void InitPools()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                var dbBidPairs = context.Bids.Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount)
                    .Select(_ => _.CurrencyPairCode).Distinct().ToList();
                var dbAskPairs = context.Asks.Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount)
                    .Select(_ => _.CurrencyPairCode).Distinct().ToList();
                var pairs = dbBidPairs.Union(dbAskPairs).Distinct().ToList();

                foreach (var pair in pairs)
                    GetPool(pair);
            }
        }

        public List<MatchingPool> GetExistingPools()
        {
            return _matchingPools.Values.ToList();
        }

        public MatchingPool GetPool(string currencyPairCode)
        {
            var matchingPool = _matchingPools.GetOrAdd(currencyPairCode, (code) => CreatePool(code));
            return matchingPool;
        }

        private MatchingPool CreatePool(string currencyPairCode)
        {
            if (string.IsNullOrWhiteSpace(currencyPairCode))
                throw new ArgumentException($"Invalid currencyPairCode '{currencyPairCode}'");

            var matchingPool = _provider.CreateInstance<MatchingPool>(currencyPairCode);
            Task.Factory.StartNew(() => matchingPool.StartAsync(new CancellationTokenSource().Token));
            return matchingPool;
        }
    }
}

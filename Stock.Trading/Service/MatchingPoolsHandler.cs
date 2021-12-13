using MatchingEngine.Data;
using MatchingEngine.Helpers;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    public class MatchingPoolsHandler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CurrenciesCache _currenciesCache;
        private readonly IServiceProvider _provider;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, MatchingPool> _matchingPools =
            new ConcurrentDictionary<string, MatchingPool>();

        private object _poolsCreationLock = new object();

        public MatchingPoolsHandler(
            IServiceScopeFactory serviceScopeFactory,
            CurrenciesCache currenciesService,
            IServiceProvider provider,
            ILogger<MatchingPool> logger)
        {
            _scopeFactory = serviceScopeFactory;
            _currenciesCache = currenciesService;
            _provider = provider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            InitPools();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ContinueWith(task => { });
            }

            // stop all pools after cancel signal
            const int defaultShutdownWaitPeriodSec = 5;
            var poolsCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(defaultShutdownWaitPeriodSec));
            await Task.WhenAll(_matchingPools.Values.Select(async pool =>
            {
                await pool.StopAsync(poolsCancellationSource.Token);
            }));
        }

        private void InitPools()
        {
            _logger.LogInformation($"InitPools begin");
            using var stopwatch = new StopwatchOperation($"InitPools",
                   (log) => _logger.LogInformation(log));
            lock (_poolsCreationLock)
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                var bids = context.Bids.AsNoTracking().Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount).ToList();  // todo use IsActive field
                var asks = context.Asks.AsNoTracking().Where(_ => !_.IsCanceled && _.Fulfilled < _.Amount).ToList();
                _logger.LogInformation($"InitPools bids:{bids.Count} asks:{asks.Count}");
                var activeOrdersByPair = bids.Cast<MatchingOrder>().Union(asks)
                    .GroupBy(_ => _.CurrencyPairCode).ToDictionary(_ => _.Key, _ => _.ToList());

                foreach (var pair in activeOrdersByPair.Keys)
                    _matchingPools[pair] = CreatePool(pair, activeOrdersByPair[pair]); // initialize pools with active orders

                var allPairs = _currenciesCache.GetCurrencyPairs();
                if (allPairs != null)
                {
                    foreach (var pair in allPairs.Select(_ => _.Code).Except(activeOrdersByPair.Keys))
                        GetPool(pair); // initialize pools that have no active orders
                }
            }
        }

        public List<MatchingPool> GetExistingPools()
        {
            return _matchingPools.Values.ToList();
        }

        public MatchingPool GetPool(string currencyPairCode)
        {
            if (_matchingPools.TryGetValue(currencyPairCode, out var pool))
                return pool;

            lock (_poolsCreationLock)
            {
                var orders = new List<MatchingOrder>(); // if pool wasn't created in InitPools() then it has no active orders
                var newPool = _matchingPools.GetOrAdd(currencyPairCode, (code) => CreatePool(code, orders));
                return newPool;
            }
        }

        private MatchingPool CreatePool(string currencyPairCode, List<MatchingOrder> activeOrders)
        {
            if (string.IsNullOrWhiteSpace(currencyPairCode))
                throw new ArgumentException($"Invalid currencyPairCode '{currencyPairCode}'");

            var matchingPool = _provider.CreateInstance<MatchingPool>(currencyPairCode, activeOrders);
            Task.Factory.StartNew(() => matchingPool.StartAsync(new CancellationTokenSource().Token));
            return matchingPool;
        }

        public async Task SendActiveOrdersToMarketData()
        {
            await Task.WhenAll(_matchingPools.Values.Select(async pool =>
            {
                pool.SendOrdersToMarketData();
            }));
        }
    }
}

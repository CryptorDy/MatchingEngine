using MatchingEngine.Data;
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
        private readonly ICurrenciesService _currenciesService;
        private readonly OrdersMatcher _ordersMatcher;
        private readonly MarketDataHolder _marketDataHolder;
        private readonly ILiquidityDeletedOrdersKeeper _liquidityDeletedOrdersKeeper;
        private readonly LiquidityExpireBlocksHandler _liquidityExpireBlocksHandler;
        private readonly IDealEndingService _dealEndingService;
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, MatchingPool> _matchingPools =
            new ConcurrentDictionary<string, MatchingPool>();

        public MatchingPoolsHandler(
            IServiceScopeFactory serviceScopeFactory,
            ICurrenciesService currenciesService,
            OrdersMatcher ordersMatcher,
            MarketDataHolder marketDataHolder,
            ILiquidityDeletedOrdersKeeper liquidityDeletedOrdersKeeper,
            LiquidityExpireBlocksHandler liquidityExpireBlocksHandler,
            IDealEndingService dealEndingService,
            IOptions<AppSettings> settings,
            ILogger<MatchingPoolsHandler> logger)
        {
            _scopeFactory = serviceScopeFactory;
            _currenciesService = currenciesService;
            _ordersMatcher = ordersMatcher;
            _marketDataHolder = marketDataHolder;
            _liquidityDeletedOrdersKeeper = liquidityDeletedOrdersKeeper;
            _liquidityExpireBlocksHandler = liquidityExpireBlocksHandler;
            _dealEndingService = dealEndingService;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            InitPools();
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(100);
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

            var matchingPool = new MatchingPool(currencyPairCode,
                _scopeFactory,
                _currenciesService,
                _ordersMatcher, _marketDataHolder, _dealEndingService,
                _liquidityDeletedOrdersKeeper, _liquidityExpireBlocksHandler,
                _settings, _logger);
            Task.Factory.StartNew(() => matchingPool.StartAsync(new CancellationTokenSource().Token));
            return matchingPool;
        }
    }
}

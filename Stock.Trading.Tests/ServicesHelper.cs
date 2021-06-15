using AutoMapper;
using MatchingEngine;
using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TLabs.ExchangeSdk.Currencies;

namespace Stock.Trading.Tests
{
    public static class ServicesHelper
    {
        public static (ServiceProvider, MatchingPoolsHandler, TradingService) CreateServiceProvider(
            Action<Order, Order> liquidityImportServiceCallback
        )
        {
            string testId = Guid.NewGuid().ToString(); // every test needs separate DB
            var services = new ServiceCollection();
            services.AddOptions();
            services.Configure<AppSettings>((_) => { });
            services.AddAutoMapper(typeof(Startup));
            services.AddDbContext<TradingDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: $"MemoryDb-{testId}"), ServiceLifetime.Transient);

            var liquidityImportService = new Mock<ILiquidityImportService>();
            liquidityImportService
                .Setup(_ => _.CreateTrade(It.IsAny<Order>(), It.IsAny<Order>()))
                .Callback(liquidityImportServiceCallback);

            services.AddTransient<SingletonsAccessor>();
            services.AddSingleton<CurrenciesCache>();

            services.AddSingleton<IHostedService, MatchingPoolsHandler>(); // if not singleton then mulitple instances are created
            services.AddTransient<TradingService>();
            services.AddSingleton<MarketDataHolder>();
            services.AddSingleton<OrdersMatcher>(_ => new OrdersMatcher(liquidityImportService.Object));
            // cant register liquidityImportService because it expects class, not interface
            services.AddSingleton<ILiquidityDeletedOrdersKeeper, LiquidityDeletedOrdersKeeper>();
            services.AddSingleton<LiquidityExpireBlocksHandler>();

            var provider = services.AddLogging(config => config.AddConsole())
                .BuildServiceProvider();

            InitCurrenciesCache(provider.GetRequiredService<CurrenciesCache>());

            return (provider,
                provider.GetRequiredService<SingletonsAccessor>().MatchingPoolsHandler,
                provider.GetRequiredService<TradingService>());
        }

        private static void InitCurrenciesCache(CurrenciesCache currenciesCache)
        {
            const int digits = 8;
            currenciesCache.SetCurrencies(new List<Currency> {
                new Currency { Code = "BTC", Digits = digits },
                new Currency { Code = "ETH", Digits = digits },
            });

            currenciesCache.SetCurrencyPairs(new List<CurrencyPair> {
                new CurrencyPair { CurrencyToId = "ETH", CurrencyFromId = "BTC", DigitsAmount = digits, DigitsPrice = digits },
            });
        }
    }
}

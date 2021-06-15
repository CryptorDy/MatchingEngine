using AutoMapper;
using MatchingEngine;
using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TLabs.ExchangeSdk.Currencies;
using Xunit;

namespace Stock.Trading.Tests
{
    public class MatchingUnitTest
    {
        public const string _currencyPairCode = "ETH_BTC";

        public Order _cheapBid = new Order(true, _currencyPairCode, 3, 10) { Id = Guid.NewGuid() };
        public Order _cheapBidWithFulfilled = new Order(true, _currencyPairCode, 3, 10) { Fulfilled = 2 };
        public Order _cheapBidWithBlocked = new Order(true, _currencyPairCode, 3, 10) { Blocked = 1 };
        public Order _cheapBidWithFulfilledBlocked = new Order(true, _currencyPairCode, 3, 10) { Fulfilled = 3, Blocked = 1 };

        public Order _cheapAsk = new Order(false, _currencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };
        public Order _cheapAskWithFulfilled = new Order(false, _currencyPairCode, 2, 10) { Fulfilled = 2 };
        public Order _cheapAskWithBlocked = new Order(false, _currencyPairCode, 2, 10) { Blocked = 1 };
        public Order _cheapAskWithFulfilledBlocked = new Order(false, _currencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };

        public Order _expensiveAsk = new Order(false, _currencyPairCode, 5, 10) { Fulfilled = 4, Blocked = 1 };

        [Fact]
        public void EmptyDataReturnEmptyResult()
        {
            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(new List<Order>(), _cheapBid.Clone());

            Assert.Empty(modifiedOrders);
            Assert.Empty(newDeals);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<Order> { _cheapBid.Clone() };

            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(pool, _expensiveAsk.Clone());

            Assert.Empty(modifiedOrders);
            Assert.Empty(newDeals);
        }

        [Fact]
        public void CorrectMatchFor2OrdersReturnOneDeal()
        {
            var ordersToMatch = new List<Order> {
                _cheapBid, _cheapAsk,
                _cheapBidWithFulfilled, _cheapAskWithFulfilled,
                _cheapBidWithBlocked, _cheapAskWithBlocked,
                _cheapBidWithFulfilledBlocked, _cheapAskWithFulfilledBlocked
            };
            for (int i = 0; i < ordersToMatch.Count; i += 2)
            {
                Console.WriteLine($"Test Match for orders pair {i / 2}");
                Order bid = ordersToMatch[0], ask = ordersToMatch[1];
                var pool = new List<Order> { bid.Clone() };

                var service = new OrdersMatcher(null);
                var (modifiedOrders, newDeals) = service.Match(pool, ask.Clone());

                decimal expectedDealVolume = Math.Min(bid.AvailableAmount, ask.AvailableAmount);
                decimal expectedDealPrice = bid.Price;
                Assert.Single(newDeals);
                Assert.Equal(expectedDealVolume, newDeals.First().Volume);
                Assert.Equal(expectedDealPrice, newDeals.First().Price);

                Order resultBid = modifiedOrders.Single(_ => _.IsBid),
                    resultAsk = modifiedOrders.Single(_ => !_.IsBid);
                Assert.Equal(bid.AvailableAmount - expectedDealVolume, resultBid.AvailableAmount);
                Assert.Equal(ask.AvailableAmount - expectedDealVolume, resultAsk.AvailableAmount);
                Assert.True(resultBid.Fulfilled <= resultBid.Amount);
                Assert.True(resultAsk.Fulfilled <= resultAsk.Amount);
                Assert.Equal(resultBid.Fulfilled < resultBid.Amount, resultBid.IsActive);
                Assert.Equal(resultAsk.Fulfilled < resultAsk.Amount, resultAsk.IsActive);
            }
        }

        [Fact]
        public void CorrectMatchFor3Orders()
        {
            var pool = new List<Order> { _cheapAskWithFulfilled.Clone(), _cheapAskWithBlocked.Clone() };

            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(pool, _cheapBid.Clone());

            Assert.Equal(2, newDeals.Count);
            Assert.Equal(3, modifiedOrders.Count);
            Order resultBid = modifiedOrders.Single(_ => _.IsBid);
            Assert.Equal(0, resultBid.AvailableAmount);
            Assert.True(resultBid.Fulfilled == resultBid.Amount);
            Assert.True(!resultBid.IsActive);
        }

        [Fact]
        public async Task CorrectMatchOfImportedOrder()
        {
            var bid = _cheapBid.Clone();
            var ask = new Order(false, _currencyPairCode, 2.5m, bid.Amount)
            {
                Id = Guid.NewGuid(),
                Exchange = Exchange.Binance,
            };
            var liquidityImportService = new Mock<ILiquidityImportService>();
            int liquidityCallbackCounter = 0;
            liquidityImportService
                .Setup(_ => _.CreateTrade(It.IsAny<Order>(), It.IsAny<Order>()))
                .Callback<Order, Order>((resultBid, resultAsk) => { liquidityCallbackCounter++; });
            var ordersMatcher = new OrdersMatcher(liquidityImportService.Object);
            var (modifiedOrders, newDeals) = ordersMatcher.Match(new List<Order> { bid.Clone() }, ask.Clone());

            Assert.Empty(newDeals);
            Assert.Equal(1, liquidityCallbackCounter);
            Assert.Equal(2, modifiedOrders.Count);
            Assert.True(modifiedOrders[0].Blocked > 0);
            Assert.Equal(0, modifiedOrders[0].AvailableAmount);
            Assert.True(modifiedOrders[1].Blocked > 0);
            Assert.Equal(0, modifiedOrders[1].AvailableAmount);
        }

        [Fact]
        public async Task DeletedOrdersDoNotGetProcessed()
        {
            async Task<int> TestProcessingWithDeletedIds(List<Order> poolOrders, List<Guid> deletedIds)
            {
                int liquidityCallbackCounter = 0;
                var (provider, matchingPoolsHandler, tradingService) =
                    CreateServiceProvider((resultBid, resultAsk) => { liquidityCallbackCounter++; });
                var matchingPool = matchingPoolsHandler.GetPool(_currencyPairCode);

                matchingPool.RemoveOrders(deletedIds);
                foreach (var order in poolOrders)
                {
                    await AddOrder(order, tradingService, matchingPool);
                }

                await Task.Delay(100);
                return liquidityCallbackCounter;
            }

            var bid = _cheapBid.Clone();
            var ask = new Order(false, _currencyPairCode, 2.5m, bid.Amount)
            {
                Id = Guid.NewGuid(),
                Exchange = Exchange.Binance,
            };
            var orders = new List<Order> { bid, ask };

            // Binance ask is skipped because it's in _liquidityDeletedOrderIds
            int matchesWithDeletedAsk = await TestProcessingWithDeletedIds(orders, new List<Guid> { ask.Id });
            Assert.Equal(0, matchesWithDeletedAsk);

            // local bid is skipped too
            int matchesWithNotDeletedBid = await TestProcessingWithDeletedIds(orders, new List<Guid> { bid.Id });
            Assert.Equal(0, matchesWithNotDeletedBid);

            // nothing should be skipped
            int matchesWithNotDeletedAsk = await TestProcessingWithDeletedIds(orders, new List<Guid> { Guid.NewGuid() });
            Assert.Equal(1, matchesWithNotDeletedAsk);
        }

        [Fact]
        public async Task CorrectResultWithExternalTrade()
        {
            var bid = _cheapBid.Clone();
            decimal totalAmount = bid.Amount;
            var ask = new Order(false, _currencyPairCode, 2.5m, totalAmount)
            {
                Id = Guid.NewGuid(),
                Exchange = Exchange.Binance,
            };

            foreach (decimal fulfilled in new List<decimal> { 0, 2, totalAmount })
            {
                await SimulateExternalTrade(bid.Clone(), ask.Clone(), fulfilled);
            }
        }

        private async Task SimulateExternalTrade(Order bid, Order ask, decimal fulfilled)
        {
            int liquidityCallbackCounter = 0;
            var (provider, matchingPoolsHandler, tradingService) =
                CreateServiceProvider((resultBid, resultAsk) => { liquidityCallbackCounter++; });
            var matchingPool = matchingPoolsHandler.GetPool(_currencyPairCode);

            // starting match with imported order
            await AddOrder(bid, tradingService, matchingPool);
            await AddOrder(ask, tradingService, matchingPool);
            await Task.Delay(1000);
            // check correct blocked value, local order updated in db, call to liquidity
            var context = provider.GetRequiredService<TradingDbContext>();
            var savedBid = context.Bids.Single();
            Assert.Equal(1, liquidityCallbackCounter);
            Assert.True(savedBid.Blocked > 0);
            Assert.Equal(0, savedBid.AvailableAmount);

            // calling from liquidity with result
            await matchingPool.UpdateExternalOrder(new ExternalCreatedOrder
            {
                IsBid = bid.IsLocal,
                TradingBidId = bid.Id.ToString(),
                TradingAskId = ask.Id.ToString(),
                MatchingEngineDealPrice = bid.DateCreated > ask.DateCreated ? ask.Price : bid.Price,
                Exchange = ask.Exchange,
                CurrencyPairCode = ask.CurrencyPairCode,
                Fulfilled = fulfilled,
            });

            context = provider.GetRequiredService<TradingDbContext>(); // context requires reload to update content
            // check correct saved result
            savedBid = context.Bids.Single();
            var generatedAsk = context.Asks.SingleOrDefault();
            var deal = context.Deals.SingleOrDefault();
            Assert.Equal(fulfilled, savedBid.Fulfilled);
            if (fulfilled == 0)
            {
                Assert.True(generatedAsk == null);
                Assert.True(deal == null);
            }
            else
            {
                Assert.True(generatedAsk != null);
                Assert.True(deal != null);
                Assert.Equal(fulfilled, generatedAsk.Fulfilled);
                Assert.Equal(fulfilled, generatedAsk.Amount);
                Assert.True(!generatedAsk.IsLocal);
                Assert.Equal(fulfilled, deal.Volume);
                Assert.Equal(savedBid.Price, deal.Price);
            }

            await matchingPool.StopAsync(new CancellationToken());
        }

        [Fact]
        public async Task DeletedOrdersInParallelDoNotGetProcessed()
        {
            // should be even numbers, to make all non-canceled orders to fully match
            const int ordersCount = 20, ordersCanceledCount = 4;

            var guidsAll = Enumerable.Range(0, ordersCount).Select(_ => Guid.NewGuid()).ToList();
            var guidsForCancel = guidsAll.OrderBy(_ => _).Take(ordersCanceledCount).ToList();

            var (provider, matchingPoolsHandler, _) =
                CreateServiceProvider((resultBid, resultAsk) => { });
            var matchingPool = matchingPoolsHandler.GetPool(_currencyPairCode);

            // add bids and asks to queue
            Parallel.For(0, guidsAll.Count, i => 
            {
                var order = _cheapBid.Clone();
                order.Id = guidsAll[i];
                order.IsBid = i % 2 == 0;
                var tradingService = provider.GetRequiredService<TradingService>();
                _ = AddOrder(order, tradingService, null);
            });

            // cancel some of them while they are in queue
            foreach (var guid in guidsForCancel) 
            {
                var tradingService = provider.GetRequiredService<TradingService>();
                var response = tradingService.CancelOrder(guid).Result;
            }
            await Task.Delay(2000);
            var context = provider.GetRequiredService<TradingDbContext>();

            // check canceled orders
            foreach (var guid in guidsForCancel)
            {
                var order = matchingPool.GetPoolOrder(guid);
                Assert.Null(order); // order should not be in pool at this point
                var dbOrder = context.GetOrder(guid).Result;
                Debug.WriteLine($"Canceled order in pool:{order}, in db:{dbOrder}");
                Assert.True((dbOrder.AvailableAmount == 0 && !dbOrder.IsCanceled) // order could be filled fully or canceled
                    || (dbOrder.AvailableAmount > 0 && dbOrder.IsCanceled)
                );
            }

            // check not canceled orders
            var orderNotCanceled = await context.GetOrder(guidsAll.Except(guidsForCancel).First());
            Debug.WriteLine($"orderNotCanceled in db:{orderNotCanceled}");
            Assert.False(orderNotCanceled.IsCanceled);
            Assert.Equal(0, orderNotCanceled.AvailableAmount);
        }

        private async Task AddOrder(Order order, TradingService tradingService,
            MatchingPool matchingPool)
        {
            if (order.IsLocal)
            {
                var request = new OrderCreateRequest
                {
                    ActionId = order.Id.ToString(),
                    IsBid = order.IsBid,
                    Price = order.Price,
                    Amount = order.Amount,
                    CurrencyPairCode = order.CurrencyPairCode,
                    DateCreated = order.DateCreated,
                    ClientType = order.ClientType,
                    UserId = order.UserId,
                    Exchange = order.Exchange,
                };
                await tradingService.CreateOrder(request);
            }
            else
            {
                matchingPool.AppendOrder(order.Clone());
            }
        }

        public (ServiceProvider, MatchingPoolsHandler, TradingService) CreateServiceProvider(Action<Order, Order> liquidityImportServiceCallback)
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

        private void InitCurrenciesCache(CurrenciesCache currenciesCache)
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

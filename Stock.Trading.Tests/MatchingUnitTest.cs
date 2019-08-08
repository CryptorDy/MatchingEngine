using AutoMapper;
using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stock.Trading.Tests.TestsV2
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
            var service = new MatchingEngine.Services.OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(new List<Order>(), _cheapBid.Clone());

            Assert.Empty(modifiedOrders);
            Assert.Empty(newDeals);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<Order> { _cheapBid.Clone() };

            var service = new MatchingEngine.Services.OrdersMatcher(null);
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

                var service = new MatchingEngine.Services.OrdersMatcher(null);
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

            var service = new MatchingEngine.Services.OrdersMatcher(null);
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
            var liquidityImportService = new Mock<MatchingEngine.Services.ILiquidityImportService>();
            int liquidityCallbackCounter = 0;
            liquidityImportService
                .Setup(_ => _.CreateTrade(It.IsAny<Order>(), It.IsAny<Order>()))
                .Callback<Order, Order>((resultBid, resultAsk) => { liquidityCallbackCounter++; });
            var ordersMatcher = new MatchingEngine.Services.OrdersMatcher(liquidityImportService.Object);
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
                await SimulateExternalTrade(bid.Clone(), ask.Clone(), fulfilled);
        }

        public async Task SimulateExternalTrade(Order bid, Order ask, decimal fulfilled)
        {
            var liquidityImportService = new Mock<MatchingEngine.Services.ILiquidityImportService>();
            int liquidityCallbackCounter = 0;
            liquidityImportService
                .Setup(_ => _.CreateTrade(It.IsAny<Order>(), It.IsAny<Order>()))
                .Callback<Order, Order>((resultBid, resultAsk) => { liquidityCallbackCounter++; });

            var dbOptions = new DbContextOptionsBuilder<TradingDbContext>()
                .UseInMemoryDatabase(databaseName: $"MemoryDb-{new Random().Next(9999)}").Options;
            using (var context = new TradingDbContext(dbOptions, GetMapper()))
            {
                var serviceProvider = new Mock<IServiceProvider>();
                serviceProvider.Setup(x => x.GetService(typeof(TradingDbContext))).Returns(context);
                var serviceScope = new Mock<IServiceScope>();
                serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
                var serviceScopeFactory = new Mock<IServiceScopeFactory>();
                serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);

                var ordersMatcher = new MatchingEngine.Services.OrdersMatcher(liquidityImportService.Object);
                var matchingPool = new MatchingEngine.Services.MatchingPool(serviceScopeFactory.Object, ordersMatcher,
                    null, null, null, null, new Mock<ILogger<MatchingEngine.Services.MatchingPool>>().Object);
                var matchingPoolAccessor = new MatchingEngine.Services.MatchingPoolAccessor(new List<IHostedService> { matchingPool });
                var tradingService = new MatchingEngine.Services.TradingService(context, matchingPoolAccessor,
                    new Mock<ILogger<MatchingEngine.Services.TradingService>>().Object);

                // starting match with imported order
                await AddOrder(bid, tradingService, matchingPool);
                await AddOrder(ask, tradingService, matchingPool);

                matchingPool.StartAsync(new CancellationToken());
                Thread.Sleep(100);

                // check correct blocked value, local order updated in db, call to liquidity
                var savedBid = context.BidsV2.Single();
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

                // check correct saved result
                savedBid = context.BidsV2.Single();
                var generatedAsk = context.AsksV2.SingleOrDefault();
                var deal = context.DealsV2.SingleOrDefault();
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
        }

        public async Task AddOrder(Order order, MatchingEngine.Services.TradingService tradingService,
            MatchingEngine.Services.MatchingPool matchingPool)
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
                    UserId = order.UserId,
                    Exchange = order.Exchange,
                    FromInnerTradingBot = order.FromInnerTradingBot,
                };
                await tradingService.CreateOrder(request);
            }
            else
            {
                matchingPool.AppendOrder(order.Clone());
            }
        }

        public IMapper GetMapper()
        {
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Order, Bid>();
                cfg.CreateMap<Order, Ask>();
            });
            mapperConfig.AssertConfigurationIsValid();
            return mapperConfig.CreateMapper();
        }
    }
}

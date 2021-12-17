using AutoMapper;
using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TLabs.ExchangeSdk;
using Xunit;

namespace Stock.Trading.Tests
{
    public class TestsLiquidityMatching
    {
        [Fact]
        public async Task CorrectMatchOfImportedOrder()
        {
            var bid = OrdersHelper.CheapBid.Clone();
            var ask = new MatchingOrder(false, OrdersHelper.CurrencyPairCode, 2.5m, bid.Amount)
            {
                Id = Guid.NewGuid(),
                Exchange = Exchange.Binance,
            };
            var liquidityImportService = new Mock<ILiquidityImportService>();
            int liquidityCallbackCounter = 0;
            liquidityImportService
                .Setup(_ => _.CreateTrade(It.IsAny<MatchingExternalTrade>()))
                .Callback(() => { liquidityCallbackCounter++; });
            var ordersMatcher = new OrdersMatcher(liquidityImportService.Object, new Mock<IMapper>().Object,
                new Mock<ILogger<OrdersMatcher>>().Object);
            var (modifiedOrders, newDeals, externalTrades) = ordersMatcher.Match(new List<MatchingOrder> { bid.Clone() }, (MatchingOrder)ask.Clone());

            Assert.Empty(newDeals);
            Assert.Equal(1, liquidityCallbackCounter);
            //Assert.Single(modifiedOrders); // initial liquidity order isn't saved, will be created in DB afterwards
            Assert.True(modifiedOrders[0].Blocked > 0);
            Assert.Equal(0, modifiedOrders[0].AvailableAmount);
            Assert.Equal(1, modifiedOrders[0].LiquidityBlocksCount);
            Assert.Single(externalTrades);
            var externalTrade = externalTrades.First();
            Assert.Equal(bid.Id, externalTrade.BidId);
            Assert.Equal(ask.Id, externalTrade.AskId);
        }

        [Fact]
        public async Task CorrectResultWithExternalTrade()
        {
            var bid = OrdersHelper.CheapBid.Clone();
            decimal totalAmount = bid.Amount;
            var ask = new MatchingOrder(false, OrdersHelper.CurrencyPairCode, 2.5m, totalAmount)
            {
                ClientType = ClientType.LiquidityBot,
                Exchange = Exchange.Binance,
            };

            foreach (decimal fulfilled in new List<decimal> { 0, 2, totalAmount })
            {
                bid.Id = Guid.NewGuid();
                ask.Id = Guid.NewGuid();
                await SimulateExternalTrade(bid.Clone(), ask.Clone(), fulfilled);
            }
        }

        private async Task SimulateExternalTrade(MatchingOrder bid, MatchingOrder ask, decimal fulfilled)
        {
            int liquidityCallbackCounter = 0;
            var (provider, matchingPoolsHandler, tradingService) =
                ServicesHelper.CreateServiceProvider(() => { liquidityCallbackCounter++; });
            var matchingPool = matchingPoolsHandler.GetPool(OrdersHelper.CurrencyPairCode);

            // starting match with imported order
            await OrdersHelper.CreateOrder(bid, tradingService, matchingPool);
            await OrdersHelper.CreateOrder(ask, tradingService, matchingPool);
            await Task.Delay(1000);
            // check correct blocked value, local order updated in db, call to liquidity
            var context = provider.GetRequiredService<TradingDbContext>();
            var savedBid = context.Bids.Single();
            Assert.Equal(1, liquidityCallbackCounter);
            Assert.True(savedBid.Blocked > 0);
            Assert.Equal(0, savedBid.AvailableAmount);
            Assert.Equal(1, savedBid.LiquidityBlocksCount);

            // calling from liquidity with result
            await matchingPool.SaveExternalTradeResult(new ExternalTrade
            {
                IsBid = bid.IsLocal,
                TradingBidId = bid.Id,
                TradingAskId = ask.Id,
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
    }
}

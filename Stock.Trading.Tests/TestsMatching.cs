using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TLabs.ExchangeSdk;
using Xunit;

namespace Stock.Trading.Tests
{
    public class TestsMatching
    {
        [Fact]
        public void EmptyDataReturnEmptyResult()
        {
            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(new List<MatchingOrder>(), OrdersHelper.CheapBid.Clone());

            Assert.Empty(modifiedOrders);
            Assert.Empty(newDeals);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<MatchingOrder> { OrdersHelper.CheapBid.Clone() };

            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(pool, OrdersHelper.ExpensiveAsk.Clone());

            Assert.Empty(modifiedOrders);
            Assert.Empty(newDeals);
        }

        [Fact]
        public void CorrectMatchFor2OrdersReturnOneDeal()
        {
            var ordersToMatch = new List<MatchingOrder> {
                OrdersHelper.CheapBid, OrdersHelper.CheapAsk,
                OrdersHelper.CheapBidWithFulfilled, OrdersHelper.CheapAskWithFulfilled,
                OrdersHelper.CheapBidWithBlocked, OrdersHelper.CheapAskWithBlocked,
                OrdersHelper.CheapBidWithFulfilledBlocked, OrdersHelper.CheapAskWithFulfilledBlocked
            };
            for (int i = 0; i < ordersToMatch.Count; i += 2)
            {
                Console.WriteLine($"Test Match for orders pair {i / 2}");
                MatchingOrder bid = ordersToMatch[0], ask = ordersToMatch[1];
                var pool = new List<MatchingOrder> { bid.Clone() };

                var service = new OrdersMatcher(null);
                var (modifiedOrders, newDeals) = service.Match(pool, ask.Clone());

                decimal expectedDealVolume = Math.Min(bid.AvailableAmount, ask.AvailableAmount);
                decimal expectedDealPrice = bid.Price;
                Assert.Single(newDeals);
                Assert.Equal(expectedDealVolume, newDeals.First().Volume);
                Assert.Equal(expectedDealPrice, newDeals.First().Price);

                MatchingOrder resultBid = modifiedOrders.Single(_ => _.IsBid),
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
            var pool = new List<MatchingOrder>
            {
                OrdersHelper.CheapAskWithFulfilled.Clone(),
                OrdersHelper.CheapAskWithBlocked.Clone()
            };

            var service = new OrdersMatcher(null);
            var (modifiedOrders, newDeals) = service.Match(pool, OrdersHelper.CheapBid.Clone());

            Assert.Equal(2, newDeals.Count);
            Assert.Equal(3, modifiedOrders.Count);
            MatchingOrder resultBid = modifiedOrders.Single(_ => _.IsBid);
            Assert.Equal(0, resultBid.AvailableAmount);
            Assert.True(resultBid.Fulfilled == resultBid.Amount);
            Assert.True(!resultBid.IsActive);
        }

        [Fact]
        public async Task DeletedOrdersDoNotGetProcessed()
        {
            async Task<int> TestProcessingWithDeletedIds(List<MatchingOrder> poolOrders, List<Guid> deletedIds)
            {
                int liquidityCallbackCounter = 0;
                var (provider, matchingPoolsHandler, tradingService) =
                    ServicesHelper.CreateServiceProvider((resultBid, resultAsk) => { liquidityCallbackCounter++; });
                var matchingPool = matchingPoolsHandler.GetPool(OrdersHelper.CurrencyPairCode);

                matchingPool.RemovePoolOrders(deletedIds);
                foreach (var order in poolOrders)
                {
                    await OrdersHelper.CreateOrder(order, tradingService, matchingPool);
                }

                await Task.Delay(100);
                return liquidityCallbackCounter;
            }

            var bid = OrdersHelper.CheapBid.Clone();
            var ask = new MatchingOrder(false, OrdersHelper.CurrencyPairCode, 2.5m, bid.Amount)
            {
                Id = Guid.NewGuid(),
                Exchange = Exchange.Binance,
            };
            var orders = new List<MatchingOrder> { bid, ask };

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
        public async Task DeletedOrdersInParallelDoNotGetProcessed()
        {
            // should be even numbers, to make all non-canceled orders to fully match
            const int ordersCount = 20, ordersCanceledCount = 4;

            var guidsAll = Enumerable.Range(0, ordersCount).Select(_ => Guid.NewGuid()).ToList();
            var guidsForCancel = guidsAll.OrderBy(_ => _).Take(ordersCanceledCount).ToList();

            var (provider, matchingPoolsHandler, _) =
                ServicesHelper.CreateServiceProvider((resultBid, resultAsk) => { });
            var matchingPool = matchingPoolsHandler.GetPool(OrdersHelper.CurrencyPairCode);

            // add bids and asks to queue
            Parallel.For(0, guidsAll.Count, i =>
            {
                var order = OrdersHelper.CheapBid.Clone();
                order.Id = guidsAll[i];
                order.IsBid = i % 2 == 0;
                var tradingService = provider.GetRequiredService<TradingService>();
                _ = OrdersHelper.CreateOrder(order, tradingService, null);
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
    }
}

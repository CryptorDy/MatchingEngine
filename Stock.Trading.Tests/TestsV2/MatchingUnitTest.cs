using MatchingEngine.Models;
using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stock.Trading.Tests.TestsV2
{
    public class MatchingUnitTest
    {
        public const string _currencyPairCode = "ETH_BTC";

        public Order _cheapBid = new Order(true, _currencyPairCode, 3, 10);
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
            var result = service.Match(new List<Order>(), _cheapBid.Clone());
            Assert.Empty(result.NewDeals);
            Assert.Empty(result.ModifiedOrders);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<Order> { _cheapBid.Clone() };

            var service = new MatchingEngine.Services.OrdersMatcher(null);

            var result = service.Match(pool, _expensiveAsk.Clone());
            Assert.Empty(result.NewDeals);
            Assert.Empty(result.ModifiedOrders);
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
                var result = service.Match(pool, ask.Clone());

                decimal expectedDealVolume = Math.Min(bid.AvailableAmount, ask.AvailableAmount);
                decimal expectedDealPrice = bid.Price;
                Assert.Single(result.NewDeals);
                Assert.Equal(expectedDealVolume, result.NewDeals.First().Volume);
                Assert.Equal(expectedDealPrice, result.NewDeals.First().Price);

                Order resultBid = result.ModifiedOrders.Single(_ => _.IsBid),
                    resultAsk = result.ModifiedOrders.Single(_ => !_.IsBid);
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
            var result = service.Match(pool, _cheapBid.Clone());

            Assert.Equal(2, result.NewDeals.Count);
            Assert.Equal(3, result.ModifiedOrders.Count);
            Order resultBid = result.ModifiedOrders.Single(_ => _.IsBid);
            Assert.Equal(0, resultBid.AvailableAmount);
            Assert.True(resultBid.Fulfilled == resultBid.Amount);
            Assert.True(!resultBid.IsActive);
        }
    }
}

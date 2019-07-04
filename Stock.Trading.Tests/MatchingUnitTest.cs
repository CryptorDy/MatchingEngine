using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stock.Trading.Tests
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
            var service = new OrdersMatcher(null);
            var result = service.Match(new List<Order>(), _cheapBid);
            Assert.Empty(result.Deals);
            Assert.Empty(result.ModifiedOrders);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<Order> { _cheapBid };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, _expensiveAsk);
            Assert.Empty(result.Deals);
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
                var pool = new List<Order> { bid };

                var service = new OrdersMatcher(null);
                var result = service.Match(pool, ask);

                decimal expectedDealVolume = Math.Min(bid.AvailableAmount, ask.AvailableAmount);
                decimal expectedDealPrice = Math.Min(bid.Price, ask.Price);
                Assert.Single(result.Deals);
                Assert.Equal(result.Deals.First().Volume, expectedDealVolume);
                Assert.Equal(result.Deals.First().Price, expectedDealPrice);

                Order resultBid = result.ModifiedOrders.Single(_ => _.IsBid),
                    resultAsk = result.ModifiedOrders.Single(_ => !_.IsBid);
                Assert.Equal(resultBid.AvailableAmount, bid.AvailableAmount - expectedDealVolume);
                Assert.Equal(resultAsk.AvailableAmount, ask.AvailableAmount - expectedDealVolume);
                Assert.True(resultBid.Fulfilled <= resultBid.Amount);
                Assert.True(resultAsk.Fulfilled <= resultAsk.Amount);
                Assert.True(resultBid.Status == (resultBid.Fulfilled == resultBid.Amount ? OrderStatus.Completed : OrderStatus.Active));
                Assert.True(resultAsk.Status == (resultAsk.Fulfilled == resultAsk.Amount ? OrderStatus.Completed : OrderStatus.Active));
            }
        }

        [Fact]
        public void CorrectMatchFor3Orders()
        {
            var pool = new List<Order> { _cheapAskWithFulfilled, _cheapAskWithBlocked };

            var service = new OrdersMatcher(null);
            var result = service.Match(pool, _cheapBid);

            Assert.True(result.Deals.Count == 2);
            Assert.True(result.ModifiedOrders.Count == 3);
            Order resultBid = result.ModifiedOrders.Single(_ => _.IsBid);
            Assert.True(resultBid.AvailableAmount == 0);
            Assert.True(resultBid.Fulfilled == resultBid.Amount);
            Assert.True(resultBid.Status == OrderStatus.Completed);
        }
    }
}

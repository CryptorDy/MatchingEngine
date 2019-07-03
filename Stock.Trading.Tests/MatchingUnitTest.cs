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

        public Order cheapBid = new Order(true, _currencyPairCode, 3, 10) { Fulfilled = 2, Blocked = 1 };
        public Order cheapAsk = new Order(false, _currencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };
        public Order expensiveAsk = new Order(false, _currencyPairCode, 5, 10) { Fulfilled = 4, Blocked = 1 };

        [Fact]
        public void CorrentMatchForSimpleImputReturnOneDeal()
        {
            var pool = new List<Order> { cheapBid };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, cheapAsk);
            Assert.Single(result.Deals);
            Assert.Equal<List<Order>>(new List<Order>() { cheapBid, cheapAsk }, result.ModifiedOrders);
        }

        [Fact]
        public void EmptyDataReturnEmptyResult()
        {
            var service = new OrdersMatcher(null);
            var result = service.Match(new List<Order>(), new Order());
            Assert.Empty(result.Deals);
            Assert.Empty(result.ModifiedOrders);
        }

        [Fact]
        public void UnmatchableOrdersDontMatch()
        {
            var pool = new List<Order> { cheapBid };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, expensiveAsk);
            Assert.Empty(result.Deals);
            Assert.Empty(result.ModifiedOrders);
        }

        [Fact]
        public void DealHasLowestPriceAndRightAmount()
        {
            var pool = new List<Order> { cheapBid };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, cheapAsk);
            Assert.Single(result.Deals);
            Assert.Equal(result.Deals.First().Price, Math.Min(cheapBid.Price, cheapAsk.Price));
            Assert.Equal(result.Deals.First().Amount, Math.Min(cheapBid.AvailableAmount, cheapAsk.AvailableAmount));
        }
    }
}

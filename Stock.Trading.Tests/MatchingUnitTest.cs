using System.Collections.Generic;
using Stock.Trading.Models;
using Stock.Trading.Service;
using Xunit;

namespace Stock.Trading.Tests
{
    public class MatchingUnitTest
    {
        [Fact]
        public void CorrentMatchForSimpleImputReturnOneDeals()
        {
            var pool = new List<MOrder>
            {
                new MAsk
                {
                    Volume = 10,
                    Fulfilled = 1,
                    Price = 2,
                    Status = MStatus.Active
                }
            };
            var newOrder = new MBid
            {
                Volume = 10,
                Fulfilled = 1,
                Price = 2,
                Status = MStatus.Active
            };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, newOrder);
            Assert.Single(result.Deals);
            Assert.Single(result.ModifiedAsks);
            Assert.Single(result.ModifiedBids);
        }

        [Fact]
        public void EmptyDataReturnEmptyResult()
        {
            var service = new OrdersMatcher(null);
            var result = service.Match(new List<MOrder>(), new MAsk());
            Assert.Empty(result.Deals);
            Assert.Empty(result.ModifiedAsks);
            Assert.Empty(result.ModifiedBids);
        }

        [Fact]
        public void Test3()
        {
            var pool = new List<MOrder>
            {
                new MAsk
                {
                    Volume = 10,
                    Fulfilled = 0,
                    Price = 2,
                    Status = MStatus.Active
                }
            };
            var newOrder = new MBid
            {
                Volume = 10,
                Fulfilled = 0,
                Price = 1,
                Status = MStatus.Active
            };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, newOrder);
            Assert.Empty(result.Deals);
            Assert.Empty(result.ModifiedAsks);
            Assert.Empty(result.ModifiedBids);
        }

        [Fact]
        public void Test4()
        {
            var pool = new List<MOrder>
            {
                new MAsk
                {
                    Volume = 10,
                    Fulfilled = 0,
                    Price = 1,
                    Status = MStatus.Active
                }
            };
            var newOrder = new MBid
            {
                Volume = 10,
                Fulfilled = 0,
                Price = 2,
                Status = MStatus.Active
            };

            var service = new OrdersMatcher(null);

            var result = service.Match(pool, newOrder);
            Assert.Single(result.Deals);
            Assert.Single(result.ModifiedAsks);
            Assert.Single(result.ModifiedBids);
        }
    }
}

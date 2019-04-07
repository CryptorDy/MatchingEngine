using Stock.Trading.Models;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Stock.Trading.Benchmark
{
    internal class Program
    {
        private static void TestDeals(int ordersCount = 100)
        {
            Console.WriteLine($"test {ordersCount} orders start");
            List<MOrder> orders = new List<MOrder>();
            var matcher = new OrdersMatcher(null);
            int deals = 0;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            for (int i = 0; i < ordersCount; i++)
            {
                var newOrder = i < ordersCount / 2
                    ? (MOrder)new MBid()
                    {
                        Created = DateTime.UtcNow,
                        CurrencyPairId = "currency_test",
                        Id = new Guid(),
                        Status = MStatus.Active,
                        Price = 1,
                        UserId = "UserId",
                        Volume = 0.01M
                    }
                    : new MAsk()
                    {
                        Created = DateTime.UtcNow,
                        CurrencyPairId = "currency_test",
                        Id = new Guid(),
                        Status = MStatus.Active,
                        Price = 1,
                        UserId = "UserId",
                        Volume = 0.01M
                    };
                var result = matcher.Match(orders, newOrder);
                orders.Add(newOrder);
                orders.RemoveAll(o => o.Status == MStatus.Completed);
                deals += result.Deals.Count;
            }
            watch.Stop();
            Console.WriteLine($"test {ordersCount} was orders done in {watch.ElapsedMilliseconds} ms. {deals} was created");
            Console.WriteLine("-----------------------------------");
        }

        private static void Main(string[] args)
        {
            TestDeals();
            TestDeals(1000);
            TestDeals(10000);
            TestDeals(20000);
            TestDeals(50000);
            TestDeals(100000);
            TestDeals(1000000);

            Console.WriteLine("Press a key");
            Console.ReadKey();
        }
    }
}

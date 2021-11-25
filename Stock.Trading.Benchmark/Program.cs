using MatchingEngine.Models;
using MatchingEngine.Services;
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
            List<MatchingOrder> orders = new List<MatchingOrder>();
            var matcher = new OrdersMatcher(null, null);
            int deals = 0;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            for (int i = 0; i < ordersCount; i++)
            {
                var newOrder = new MatchingOrder()
                {
                    Id = new Guid(),
                    IsBid = i < ordersCount / 2,
                    Price = 1,
                    Amount = 0.01M,
                    CurrencyPairCode = "currency_test",
                    DateCreated = DateTimeOffset.UtcNow,
                    UserId = "UserId",
                };
                var (modifiedOrders, newDeals) = matcher.Match(orders, newOrder);
                orders.Add(newOrder);
                orders.RemoveAll(o => !o.IsActive);
                deals += newDeals.Count;
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

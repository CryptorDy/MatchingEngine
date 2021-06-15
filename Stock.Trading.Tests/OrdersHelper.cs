using MatchingEngine.Models;
using MatchingEngine.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Tests
{
    public static class OrdersHelper
    {
        public const string CurrencyPairCode = "ETH_BTC";

        public static Order CheapBid = new Order(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid() };
        public static Order CheapBidWithFulfilled = new Order(true, CurrencyPairCode, 3, 10) { Fulfilled = 2 };
        public static Order CheapBidWithBlocked = new Order(true, CurrencyPairCode, 3, 10) { Blocked = 1 };
        public static Order CheapBidWithFulfilledBlocked = new Order(true, CurrencyPairCode, 3, 10) { Fulfilled = 3, Blocked = 1 };

        public static Order CheapAsk = new Order(false, CurrencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };
        public static Order CheapAskWithFulfilled = new Order(false, CurrencyPairCode, 2, 10) { Fulfilled = 2 };
        public static Order CheapAskWithBlocked = new Order(false, CurrencyPairCode, 2, 10) { Blocked = 1 };
        public static Order CheapAskWithFulfilledBlocked = new Order(false, CurrencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };

        public static Order ExpensiveAsk = new Order(false, CurrencyPairCode, 5, 10) { Fulfilled = 4, Blocked = 1 };

        public static async Task CreateOrder(Order order, TradingService tradingService,
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
    }
}

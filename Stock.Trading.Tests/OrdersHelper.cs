using MatchingEngine.Models;
using MatchingEngine.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using TLabs.ExchangeSdk.Trading;

namespace Stock.Trading.Tests
{
    public static class OrdersHelper
    {
        public const string CurrencyPairCode = "ETH_BTC";

        public static MatchingOrder CheapBid = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid() };
        public static MatchingOrder CheapBidWithFulfilled = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Fulfilled = 2 };
        public static MatchingOrder CheapBidWithBlocked = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Blocked = 1 };
        public static MatchingOrder CheapBidWithFulfilledBlocked = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Fulfilled = 3, Blocked = 1 };

        public static MatchingOrder CheapAsk = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };
        public static MatchingOrder CheapAskWithFulfilled = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Fulfilled = 2 };
        public static MatchingOrder CheapAskWithBlocked = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Blocked = 1 };
        public static MatchingOrder CheapAskWithFulfilledBlocked = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Fulfilled = 3, Blocked = 1 };

        public static MatchingOrder ExpensiveAsk = new MatchingOrder(false, CurrencyPairCode, 5, 10) { Fulfilled = 4, Blocked = 1 };

        public static async Task CreateOrder(MatchingOrder order, TradingService tradingService,
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
                matchingPool.AddNewOrder(order.Clone());
            }
        }
    }
}

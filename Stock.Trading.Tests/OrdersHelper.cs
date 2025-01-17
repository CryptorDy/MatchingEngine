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

        public static MatchingOrder CheapBid = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid(), UserId = "a", };
        public static MatchingOrder CheapBidWithFulfilled = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 2, };
        public static MatchingOrder CheapBidWithBlocked = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid(), UserId = "a", Blocked = 1 };
        public static MatchingOrder CheapBidWithFulfilledBlocked = new MatchingOrder(true, CurrencyPairCode, 3, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 3, Blocked = 1 };

        public static MatchingOrder CheapAsk = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 3, Blocked = 1 };
        public static MatchingOrder CheapAskWithFulfilled = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 2 };
        public static MatchingOrder CheapAskWithBlocked = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Id = Guid.NewGuid(), UserId = "a",
            Blocked = 1 };
        public static MatchingOrder CheapAskWithFulfilledBlocked = new MatchingOrder(false, CurrencyPairCode, 2, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 3, Blocked = 1 };

        public static MatchingOrder ExpensiveAsk = new MatchingOrder(false, CurrencyPairCode, 5, 10) { Id = Guid.NewGuid(), UserId = "a",
            Fulfilled = 4, Blocked = 1 };

        public static async Task CreateOrder(MatchingOrder order, TradingService tradingService,
            MatchingPool matchingPool)
        {
            if (order.IsLocal)
            {
                var request = new TLabs.ExchangeSdk.Trading.OrderCreateRequest
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
                var o = order.Clone();
                matchingPool.EnqueuePoolAction(PoolActionType.CreateLiquidityOrder, o.Id, o);
            }
        }
    }
}

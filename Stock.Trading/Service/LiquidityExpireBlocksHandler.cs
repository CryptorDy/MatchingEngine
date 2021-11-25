using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class LiquidityExpireBlocksHandler
    {
        private MatchingPoolsHandler _matchingPoolsHandler;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<Guid, OrderBlockingInfo> _orderBlockings =
            new ConcurrentDictionary<Guid, OrderBlockingInfo>();

        public LiquidityExpireBlocksHandler(ILogger<LiquidityExpireBlocksHandler> logger)
        {
            _logger = logger;
        }

        public void SetMatchingPoolsHandler(MatchingPoolsHandler matchingPoolsHandler)
        {
            _matchingPoolsHandler = matchingPoolsHandler;
        }

        public async Task CheckBlockings()
        {
            if (_matchingPoolsHandler == null)
                return;

            foreach (var blocking in _orderBlockings.Values.ToList())
            {
                if (blocking.DateBlocked > DateTimeOffset.UtcNow.AddMinutes(-1))
                    continue;

                _matchingPoolsHandler.GetPool(blocking.CurrencyPairCode)
                    .EnqueuePoolAction(PoolActionType.AutoUnblock, blocking.OrderId);
                Remove(blocking.OrderId);
            }
        }

        public void Add(Guid orderId, string currencyPairCode)
        {
            _orderBlockings[orderId] = new OrderBlockingInfo
            {
                OrderId = orderId,
                CurrencyPairCode = currencyPairCode,
                DateBlocked = DateTimeOffset.UtcNow,
            };
        }

        public void Remove(Guid orderId)
        {
            _orderBlockings.TryRemove(orderId, out _);
        }
    }
}

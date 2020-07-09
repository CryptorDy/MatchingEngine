using MatchingEngine.Models.LiquidityImport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class LiquidityExpireBlocksWatcher : BackgroundService
    {
        private MatchingPool _matchingPool;
        private readonly ILogger _logger;

        private List<OrderBlockingInfo> OrderBlockings = new List<OrderBlockingInfo>();

        public LiquidityExpireBlocksWatcher(ILogger<LiquidityExpireBlocksWatcher> logger)
        {
            _logger = logger;
        }

        public void SetMatchingPool(MatchingPool matchingPool)
        {
            _matchingPool = matchingPool;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1 * 60 * 1000);

                try
                {
                    await CheckBlockings();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "");
                }
            }
        }

        public async Task CheckBlockings()
        {
            List<Guid> orderIdsToUnblock;
            lock (OrderBlockings)
            {
                orderIdsToUnblock = OrderBlockings.Where(_ => _.DateBlocked < DateTime.Now.AddMinutes(-1))
                    .Select(_ => _.OrderId).Distinct()
                    .ToList();
            }
            await _matchingPool.UnblockOrders(orderIdsToUnblock); // unblock old blocked orders and remove ids from OrderBlocks
        }

        public void Add(Guid orderId)
        {
            lock (OrderBlockings)
            {
                OrderBlockings.Add(new OrderBlockingInfo { OrderId = orderId, DateBlocked = DateTime.Now });
            }
        }

        public void Remove(Guid orderId)
        {
            lock (OrderBlockings)
            {
                OrderBlockings.RemoveAll(_ => _.OrderId == orderId);
            }
        }
    }
}

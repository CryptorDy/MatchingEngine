using MatchingEngine.Data;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class TradingService
    {
        private readonly TradingDbContext _context;
        private readonly MatchingPool _matchingPool;
        private readonly ILogger _logger;

        public TradingService(TradingDbContext context,
            MatchingPoolAccessor matchingPoolAccessor,
            ILogger<TradingService> logger)
        {
            _context = context;
            _matchingPool = matchingPoolAccessor.MatchingPool;
            _logger = logger;
        }

        #region GET-requests

        public async Task<List<Order>> GetOrders(bool isBid, string userId = null)
        {
            try
            {
                var result = await _context.GetOrders(isBid, userId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return new List<Order>();
            }
        }

        public async Task<Order> GetOrder(bool? isBid, string id)
        {
            try
            {
                var result = await _context.GetOrder(isBid, Guid.Parse(id));
                foreach (var deal in result.DealList)
                {
                    deal.Ask = null;
                    deal.Bid = null;
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
                return null;
            }
        }

        public async Task<List<Deal>> GetDeals(string currencyPairCode, int? lastNum, string userId,
            DateTimeOffset? sinceDate = null, List<string> dealIds = null)
        {
            try
            {
                var deals = await _context.Deals
                    .Include(m => m.Ask)
                    .Include(m => m.Bid)
                    .Where(_ => (!sinceDate.HasValue || _.DateCreated > sinceDate)
                        && (string.IsNullOrWhiteSpace(currencyPairCode) || (_.Bid != null && _.Bid.CurrencyPairCode == currencyPairCode))
                        && (string.IsNullOrWhiteSpace(userId) || (_.Bid != null && _.Bid.UserId == userId) || (_.Ask != null && _.Ask.UserId == userId))
                        && (dealIds == null || dealIds.Count == 0 || dealIds.Contains(_.DealId.ToString())))
                    .OrderByDescending(m => m.DateCreated)
                    .Take(lastNum ?? int.MaxValue)
                    .ToListAsync();

                foreach (var deal in deals)
                {
                    deal.RemoveCircularDependency();
                }
                return deals;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return new List<Deal>();
            }
        }

        public Deal GetDeal(string id)
        {
            try
            {
                var deal = _context.Deals
                    .Include(d => d.Ask)
                    .Include(d => d.Bid)
                    .FirstOrDefault(o => o.DealId == Guid.Parse(id));
                if (deal != null)
                {
                    deal.RemoveCircularDependency();
                }
                return deal;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return null;
            }
        }

        public DealResponse GetDealResponse(string id)
        {
            try
            {
                var deal = GetDeal(id);
                var dealResponse = deal.GetDealResponse();
                return dealResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return null;
            }
        }

        #endregion GET-requests

        public async Task<Guid> CreateOrder(OrderCreateRequest request)
        {
            var order = new Order
            {
                Id = new Guid(request.ActionId),
                IsBid = request.IsBid,
                Price = request.Price,
                Amount = request.Amount,
                CurrencyPairCode = request.CurrencyPairCode,
                DateCreated = request.DateCreated,
                ClientType = request.ClientType,
                UserId = request.UserId,
                Exchange = request.Exchange,
            };

            if (order.ClientType != ClientType.DealsBot)
            {
                await _context.AddOrder(order, true);
            }

            _matchingPool.AppendOrder(order);

            return order.Id;
        }

        public async Task<int> DeleteOrder(bool isBid, string id, string userId)
        {
            try
            {
                int result = 0;
                var order = await _context.GetOrder(isBid, Guid.Parse(id));

                if (order != null && order.UserId == userId)
                {
                    order.IsCanceled = true;
                    result = await _context.SaveChangesAsync();
                }
                await _matchingPool.RemoveOrder(Guid.Parse(id));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Delete order error", ex);
                return 0;
            }
        }
    }
}

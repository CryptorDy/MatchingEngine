using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stock.Trading.Data;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Requests;
using Stock.Trading.Responses;
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

        public async Task<List<MatchingEngine.Models.Order>> GetOrders(string userId = null)
        {
            try
            {
                var result = await _context.GetOrders(userId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return new List<MatchingEngine.Models.Order>();
            }
        }

        public async Task<MatchingEngine.Models.Order> GetOrder(bool isBid, string id)
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

        public async Task<List<MatchingEngine.Models.Deal>> GetDeals(string currencyPairCode, int? lastNum, string userId,
            DateTimeOffset? sinceDate = null, List<string> dealIds = null)
        {
            try
            {
                var deals = await _context.DealsV2
                    .Include(m => m.Ask)
                    .Include(m => m.Bid)
                    .Where(_ => (!sinceDate.HasValue || _.DateCreated > sinceDate)
                        && (string.IsNullOrWhiteSpace(currencyPairCode) || (_.Bid != null && _.Bid.CurrencyPairCode == currencyPairCode))
                        && (string.IsNullOrWhiteSpace(userId) || (_.Bid != null && _.Bid.UserId == userId) || (_.Ask != null && _.Ask.UserId == userId))
                        && (dealIds == null || dealIds.Count == 0 || dealIds.Contains(_.DealId.ToString())))
                    .OrderByDescending(m => m.DateCreated)
                    .Take(lastNum ?? int.MaxValue)
                    .ToListAsync();

                foreach (var deal in deals) // remove circular dependency to prevent json error
                {
                    if (deal.Bid != null)
                        deal.Bid.DealList = null;
                    if (deal.Ask != null)
                        deal.Ask.DealList = null;
                }
                return deals;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return new List<MatchingEngine.Models.Deal>();
            }
        }

        public MatchingEngine.Models.Deal GetDeal(string id)
        {
            try
            {
                var deal = _context.DealsV2
                    .Include(d => d.Ask)
                    .Include(d => d.Bid)
                    .FirstOrDefault(o => o.DealId == Guid.Parse(id));
                if (deal != null)
                {
                    if (deal.Ask != null)
                        deal.Ask.DealList = null;
                    if (deal.Bid != null)
                        deal.Bid.DealList = null;
                }
                return deal;
            }
            catch (Exception ex)
            {
                _logger.LogError("", ex);
                return null;
            }
        }

        #endregion GET-requests

        public async Task<Guid> CreateOrder(AddOrderDto dto)
        {
            var order = new MatchingEngine.Models.Order
            {
                Id = dto.Id,
                IsBid = dto.IsBid,
                Price = dto.Price,
                Amount = dto.Amount,
                CurrencyPairCode = dto.CurrencyPairCode,
                DateCreated = dto.DateCreated,
                UserId = dto.UserId,
                Exchange = dto.Exchange,
                FromInnerTradingBot = dto.FromInnerTradingBot,
            };

            if (!order.FromInnerTradingBot)
                await _context.AddOrder(order, true);
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

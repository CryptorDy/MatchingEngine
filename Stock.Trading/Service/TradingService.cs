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

namespace Stock.Trading.Service
{
    public class TradingService
    {
        private readonly TradingDbContext _context;
        private readonly ILogger _logger;
        private readonly MatchingPool _matchingPool;

        public TradingService(TradingDbContext context,
            MatchingPoolAccessor matchingPoolAccessor,
            ILogger<TradingService> logger)
        {
            _context = context;
            _logger = logger;
            _matchingPool = matchingPoolAccessor.MatchingPool;
        }

        #region GET-requests

        public async Task<List<Ask>> Asks(string userId = null)
        {
            List<Ask> result = new List<Ask>();

            try
            {
                result = await _context.Asks
                    .Include(o => o.OrderType)
                    .Where(_ => string.IsNullOrEmpty(userId) || _.UserId == userId)
                    .ToAsyncEnumerable()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }

            return result;
        }

        public async Task<Ask> Ask(string id)
        {
            try
            {
                var result = await _context.Asks
                    .Include(o => o.OrderType)
                    .Include(o => o.DealList)
                    .SingleOrDefaultAsync(_ => _.Id == Guid.Parse(id));
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

        public async Task<List<Bid>> Bids(string userId = null)
        {
            List<Bid> result = new List<Bid>();

            try
            {
                result = await _context.Bids
                    .Include(o => o.OrderType)
                    .Where(_ => string.IsNullOrEmpty(userId) || _.UserId == userId)
                    .ToAsyncEnumerable()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }

            return result;
        }

        public async Task<Bid> Bid(string id)
        {
            try
            {
                var result = await _context.Bids
                    .Include(o => o.OrderType)
                    .Include(o => o.DealList)
                    .SingleOrDefaultAsync(_ => _.Id == Guid.Parse(id));
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

        public AskResponse GetAsk(string id)
        {
            try
            {
                var response = _context.Asks
                    .Include(m => m.DealList)
                    .Where(o => o.Id == Guid.Parse(id))
                    .Select(order => new AskResponse
                    {
                        OrderDate = order.OrderDateUtc,
                        Price = order.Price,
                        Amount = order.Volume,
                        RemainingAmount = order.Volume - order.DealList.Select(_ => _.Volume).DefaultIfEmpty(0).Sum(),
                        Id = order.Id.ToString(),
                        UserId = order.UserId,
                        OrderTypeId = order.OrderTypeCode,
                        CurrencyPairId = order.CurrencyPairId
                    }).FirstOrDefault();

                if (response == null)
                {
                    return new AskResponse();
                }
                return response;
                //return new AskResponse {
                //    OrderDate = order.OrderDateUtc,
                //    Price = order.Price,
                //    Amount = order.Volume,
                //    RemainingAmount = order.Volume - order.DealList.Sum(d => d.Volume),
                //    Id = order.Id.ToString(),
                //    UserId = order.UserId,
                //    OrderTypeId = order.OrderTypeCode,
                //    CurrencyPairId = order.CurrencyPairId
                //};
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "0");
            }

            return new AskResponse();
        }

        public BidResponse GetBid(string id)
        {
            try
            {
                var response = _context.Bids
                    .Include(m => m.DealList)
                    .Where(o => o.Id.ToString() == id)
                    .Select(order => new BidResponse
                    {
                        OrderDate = order.OrderDateUtc,
                        Price = order.Price,
                        Amount = order.Volume,
                        RemainingAmount = order.Volume - order.DealList.Select(_ => _.Volume).DefaultIfEmpty(0).Sum(),
                        RemainingReservationAmount = order.Volume * order.Price - order.DealList.Select(_ => _.Price * _.Volume).DefaultIfEmpty(0).Sum(),
                        Id = order.Id.ToString(),
                        UserId = order.UserId,
                        OrderTypeId = order.OrderTypeCode,
                        CurrencyPairId = order.CurrencyPairId
                    }).FirstOrDefault();

                if (response == null)
                {
                    return new BidResponse();
                }
                return response;
                //return new BidResponse {
                //    OrderDate = order.OrderDateUtc,
                //    Price = order.Price,
                //    Amount = order.Volume,
                //    RemainingAmount = order.Volume - order.DealList.Sum(d => d.Volume),
                //    Id = order.Id.ToString(),
                //    UserId = order.UserId,
                //    OrderTypeId = order.OrderTypeCode,
                //    CurrencyPairId = order.CurrencyPairId
                //};
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "0");
            }

            return new BidResponse();
        }

        public async Task<List<Deal>> GetDeals(string currencyPairId, int? lastNum, string userId,
            DateTime? sinceDate = null, List<string> dealIds = null)
        {
            try
            {
                var allDeals = await _context.Deals
                    .Include(m => m.Ask)
                    .Include(m => m.Bid)
                    .Where(_ => (!sinceDate.HasValue || _.DealDateUtc > sinceDate)
                        && (string.IsNullOrWhiteSpace(currencyPairId) || (_.Bid != null && _.Bid.CurrencyPairId == currencyPairId))
                        && (string.IsNullOrWhiteSpace(userId) || (_.Bid != null && _.Bid.UserId == userId) || (_.Ask != null && _.Ask.UserId == userId))
                        && (dealIds == null || dealIds.Count == 0 || dealIds.Contains(_.DealId.ToString())))
                    .OrderByDescending(m => m.DealDateUtc)
                    .ThenBy(m => m.DealDateUtc)
                    .ToListAsync();

                if (lastNum.HasValue)
                    allDeals = allDeals.Take(lastNum.Value).ToList();

                foreach (var deal in allDeals) // remove circular dependency
                {
                    if (deal.Bid != null)
                        deal.Bid.DealList = null;
                    if (deal.Ask != null)
                        deal.Ask.DealList = null;
                }
                return allDeals;
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
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
                    .FirstOrDefault(o => o.DealId.ToString() == id);
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
                _logger.LogError("0", ex);
                return null;
            }
        }

        public DealResponse GetDealResponse(string id)
        {
            try
            {
                var deal = GetDeal(id);

                if (deal == null)
                {
                    return new DealResponse();
                }

                return new DealResponse
                {
                    DealId = deal.DealId,
                    DealDateUtc = deal.DealDateUtc,
                    Price = deal.Price,
                    Volume = deal.Volume,
                    FromInnerTradingBot = deal.FromInnerTradingBot,
                    AskId = deal.Ask.Id,
                    BidId = deal.Bid.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
            }
            return new DealResponse();
        }

        #endregion GET-requests

        public async Task<Guid> CreateAsk(AddRequest request)
        {
            var ask = new Ask
            {
                Id = new Guid(request.ActionId),
                Volume = request.Amount,
                Price = request.Price,
                OrderDateUtc = request.OrderDateUtc,
                CurrencyPairId = request.CurrencyPariId, //todo: fixme
                UserId = request.UserId,
                ExchangeId = request.ExchangeId,
                FromInnerTradingBot = request.FromInnerTradingBot,
                OrderTypeCode = OrderType.Active.Code
            };

            if (!ask.FromInnerTradingBot)
            {
                _context.Asks.Add(ask);
                await _context.SaveChangesAsync();
            }

            MAsk ma = new MAsk()
            {
                Id = ask.Id,
                UserId = ask.UserId,
                Volume = ask.Volume,
                Fulfilled = ask.Fulfilled,
                Price = ask.Price,
                Created = ask.OrderDateUtc,
                CurrencyPairId = ask.CurrencyPairId,
                ExchangeId = ask.ExchangeId,
                FromInnerTradingBot = ask.FromInnerTradingBot,
                Status = MStatus.Active
            };

            _matchingPool.AppendAsk(ma);

            return ask.Id;
        }

        public async Task<int> DeleteAskAsync(string id, string userId)
        {
            try
            {
                int res = 0;
                var order = await _context.Asks
                    .SingleOrDefaultAsync(o => o.Id.ToString() == id);

                if (order != null && order.UserId == userId)
                {
                    order.OrderTypeCode = OrderType.Canceled.Code;
                    res = await _context.SaveChangesAsync();
                }
                await _matchingPool.RemoveAsk(Guid.Parse(id));
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError("Delete ask error", ex);
                return 0;
            }
        }

        public async Task<Guid> CreateBid(AddRequest request)
        {
            var bid = new Bid
            {
                Id = new Guid(request.ActionId),
                Volume = request.Amount,
                Price = request.Price,
                OrderDateUtc = request.OrderDateUtc,
                CurrencyPairId = request.CurrencyPariId,
                UserId = request.UserId,
                ExchangeId = request.ExchangeId,
                FromInnerTradingBot = request.FromInnerTradingBot,
                OrderTypeCode = OrderType.Active.Code
            };

            if (!bid.FromInnerTradingBot)
            {
                _context.Bids.Add(bid);
                await _context.SaveChangesAsync();
            }

            MBid mb = new MBid()
            {
                Id = bid.Id,
                UserId = bid.UserId,
                Volume = bid.Volume,
                Fulfilled = bid.Fulfilled,
                Price = bid.Price,
                Created = bid.OrderDateUtc,
                CurrencyPairId = bid.CurrencyPairId,
                ExchangeId = bid.ExchangeId,
                FromInnerTradingBot = bid.FromInnerTradingBot,
                Status = MStatus.Active
            };

            _matchingPool.AppendBid(mb);

            return bid.Id;
        }

        public async Task<int> DeleteBidAsync(string id, string userId)
        {
            try
            {
                int res = 0;
                var order = await _context.Bids.SingleOrDefaultAsync(o => o.Id == Guid.Parse(id));
                if (order != null && order.UserId == userId)
                {
                    order.OrderTypeCode = OrderType.Canceled.Code;
                    res = await _context.SaveChangesAsync();
                }
                await _matchingPool.RemoveBid(Guid.Parse(id));
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError("Delete bid error", ex);
                return 0;
            }
        }
    }
}

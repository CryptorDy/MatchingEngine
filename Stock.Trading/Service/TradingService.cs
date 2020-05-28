using MatchingEngine.Data;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class TradingService
    {
        private readonly TradingDbContext _context;
        private readonly ICurrenciesService _currenciesService;
        private readonly MatchingPool _matchingPool;
        private readonly ILogger _logger;

        public TradingService(TradingDbContext context,
            ICurrenciesService currenciesService,
            SingletonsAccessor singletonsAccessor,
            ILogger<TradingService> logger)
        {
            _context = context;
            _currenciesService = currenciesService;
            _matchingPool = singletonsAccessor.MatchingPool;
            _logger = logger;
        }

        #region GET-requests

        public async Task<Order> GetOrder(bool? isBid, Guid id)
        {
            try
            {
                var order = _matchingPool.GetPoolOrder(id);
                if (order == null)
                {
                    order = await _context.GetOrder(isBid, id);
                }
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError("0", ex);
                return null;
            }
        }

        public async Task<List<Deal>> GetDeals(string currencyPairCode, int? lastNum, string userId,
            DateTimeOffset? sinceDate = null, DateTimeOffset? toDate = null, List<string> dealIds = null)
        {
            try
            {
                var deals = await _context.Deals
                    .Include(m => m.Ask)
                    .Include(m => m.Bid)
                    .Where(_ =>
                        (!sinceDate.HasValue || _.DateCreated >= sinceDate)
                        &&
                        (!toDate.HasValue || _.DateCreated < toDate)
                        &&
                        (string.IsNullOrWhiteSpace(currencyPairCode) || (_.Bid != null && _.Bid.CurrencyPairCode == currencyPairCode))
                        &&
                        (string.IsNullOrWhiteSpace(userId) || (_.Bid != null && _.Bid.UserId == userId) || (_.Ask != null && _.Ask.UserId == userId))
                        &&
                        (dealIds == null || dealIds.Count == 0 || dealIds.Contains(_.DealId.ToString()))
                        )
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
            request.Price = Math.Round(request.Price, _currenciesService.GetPriceDigits(request.CurrencyPairCode));
            request.Amount = Math.Round(request.Amount, _currenciesService.GetAmountDigits(request.CurrencyPairCode));
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

        public async Task<CancelOrderResponse> DeleteOrder(bool isBid, Guid id, string userId)
        {
            try
            {
                Console.WriteLine($"DeleteOrder() start. id:{id} userId:{userId}");
                var order = await GetOrder(isBid, id);
                if (order == null)
                {
                    Console.WriteLine("DeleteOrder() Not found.");
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
                }
                Console.WriteLine($"DeleteOrder() {order}");
                if (order.UserId != userId)
                {
                    Console.WriteLine($"DeleteOrder() wrong userId");
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
                }
                if (order.Blocked > 0)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.LiquidityBlocked };
                }
                if (order.IsCanceled)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyCanceled };
                }
                if (order.Fulfilled >= order.Amount)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyFilled };
                }

                order = await _context.GetOrder(isBid, id);
                order.IsCanceled = true;
                await _context.UpdateOrder(order, true);
                await _matchingPool.RemoveOrder(id);
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Success, Order = order };
            }
            catch (Exception ex)
            {
                _logger.LogError("Delete order error", ex);
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
            }
        }
    }
}

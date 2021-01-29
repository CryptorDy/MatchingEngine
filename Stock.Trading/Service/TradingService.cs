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
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly ICurrenciesService _currenciesService;
        private readonly ILogger _logger;

        public TradingService(TradingDbContext context,
            SingletonsAccessor singletonsAccessor,
            ICurrenciesService currenciesService,
            ILogger<TradingService> logger)
        {
            _context = context;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _currenciesService = currenciesService;
            _logger = logger;
        }

        #region GET-requests

        public async Task<Order> GetOrder(Guid orderId, bool? isBid = null)
        {
            var order = await _context.GetOrder(orderId, isBid);
            return order;
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
            var order = request.GetOrder();

            if (order.ClientType != ClientType.DealsBot)
            {
                await _context.AddOrder(order, true, OrderEventType.Create);
            }

            _matchingPoolsHandler.GetPool(request.CurrencyPairCode).AppendOrder(order);

            return order.Id;
        }

        public async Task<CancelOrderResponse> DeleteOrder(Guid orderId)
        {
            try
            {
                Console.WriteLine($"DeleteOrder() start. id:{orderId}");
                var order = await GetOrder(orderId);
                if (order == null)
                {
                    Console.WriteLine("DeleteOrder() Not found.");
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
                }
                Console.WriteLine($"DeleteOrder() {order}");
                if (order.Blocked > 0)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.LiquidityBlocked, Order = order };
                }
                if (order.IsCanceled)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyCanceled, Order = order };
                }
                if (order.Fulfilled >= order.Amount)
                {
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.AlreadyFilled, Order = order };
                }

                order = await _context.GetOrder(orderId);
                order.IsCanceled = true;
                await _context.UpdateOrder(order, true, OrderEventType.Cancel);
                await _matchingPoolsHandler.GetPool(order.CurrencyPairCode).RemoveOrder(orderId);
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

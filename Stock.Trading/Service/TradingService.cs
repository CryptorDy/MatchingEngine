using MatchingEngine.Data;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    public class TradingService
    {
        private readonly TradingDbContext _context;
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly CurrenciesCache _currenciesCache;
        private readonly ILogger _logger;

        public TradingService(TradingDbContext context,
            SingletonsAccessor singletonsAccessor,
            CurrenciesCache currenciesService,
            ILogger<TradingService> logger)
        {
            _context = context;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _currenciesCache = currenciesService;
            _logger = logger;
        }

        #region GET-requests

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
            if (_currenciesCache.GetCurrencyPair(request.CurrencyPairCode) != null)
            {
                request.Price = Math.Round(request.Price, _currenciesCache.GetPriceDigits(request.CurrencyPairCode));
                request.Amount = Math.Round(request.Amount, _currenciesCache.GetAmountDigits(request.CurrencyPairCode));
            }
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
                _logger.LogDebug($"DeleteOrder() start. id:{orderId}");
                var dbOrder = await _context.GetOrder(orderId);
                if (dbOrder == null)
                {
                    _logger.LogDebug("DeleteOrder() Not found.");
                    return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
                }

                var pool = _matchingPoolsHandler.GetPool(dbOrder.CurrencyPairCode);
                var order = pool.GetPoolOrder(orderId);
                _logger.LogDebug($"DeleteOrder() {dbOrder}");

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

                pool.RemoveOrder(orderId);
                dbOrder = await _context.GetOrder(orderId); // reload order from db after it was removed from pool
                dbOrder.IsCanceled = true;
                await _context.UpdateOrder(dbOrder, true, OrderEventType.Cancel);
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Success, Order = dbOrder };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete order {orderId} error", ex);
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
            }
        }
    }
}

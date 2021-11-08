using AutoMapper;
using MatchingEngine.Data;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Currencies;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Services
{
    public class TradingService
    {
        private readonly TradingDbContext _context;
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly CurrenciesCache _currenciesCache;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        public TradingService(TradingDbContext context,
            SingletonsAccessor singletonsAccessor,
            CurrenciesCache currenciesService,
            IMapper mapper,
            ILogger<TradingService> logger)
        {
            _context = context;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _currenciesCache = currenciesService;
            _mapper = mapper;
            _logger = logger;
        }

        #region GET-requests

        public async Task<List<Deal>> GetDeals(string currencyPairCode, int? lastNum, List<string> userIds = null,
            DateTimeOffset? sinceDate = null, DateTimeOffset? toDate = null, List<string> dealIds = null)
        {
            try
            {
                userIds = userIds?.Where(_ => _.HasValue()).ToList(); // remove empty values
                var query = _context.Deals
                    .Include(m => m.Ask)
                    .Include(m => m.Bid)
                    .Where(_ =>
                        (!sinceDate.HasValue || _.DateCreated >= sinceDate)
                        &&
                        (!toDate.HasValue || _.DateCreated < toDate)
                        &&
                        (string.IsNullOrWhiteSpace(currencyPairCode) || (_.Bid != null && _.Bid.CurrencyPairCode == currencyPairCode))
                        &&
                        (dealIds == null || dealIds.Count == 0 || dealIds.Contains(_.DealId.ToString()))
                        );
                if (userIds?.Count > 0)
                    query = query.Where(_ => userIds.Contains(_.Bid.UserId) || userIds.Contains(_.Ask.UserId));

                var deals = await query.OrderByDescending(m => m.DateCreated)
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

        public MarketdataDeal GetDealResponse(string id)
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
            var order = _mapper.Map<Models.MatchingOrder>(request.GetOrder());

            if (order.ClientType != ClientType.DealsBot)
            {
                await _context.AddOrder(order, true, OrderEventType.Create);
            }

            _matchingPoolsHandler.GetPool(request.CurrencyPairCode).AddNewOrder(order);

            return order.Id;
        }

        public async Task<CancelOrderResponse> CancelOrder(Guid orderId)
        {
            try
            {
                var dbOrder = await _context.GetOrder(orderId);
                var pool = _matchingPoolsHandler.GetPool(dbOrder.CurrencyPairCode);
                var result = pool.CancelOrder(orderId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete order {orderId} error", ex);
                return new CancelOrderResponse { Status = CancelOrderResponseStatus.Error };
            }
        }
    }
}

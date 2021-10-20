using Flurl.Http;
using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Controllers
{
    [Route("api/[controller]")]
    public class OrderController : Controller
    {
        private readonly TradingDbContext _context;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly TradingService _tradingService;
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger _logger;

        public OrderController(TradingDbContext context,
            GatewayHttpClient gatewayHttpClient,
            TradingService tradingService,
            SingletonsAccessor singletonsAccessor,
            MarketDataService marketDataService,
            ILogger<OrderController> logger)
        {
            _context = context;
            _gatewayHttpClient = gatewayHttpClient;
            _tradingService = tradingService;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        /// <summary>
        /// Get orders list
        /// </summary>
        [HttpGet]
        public async Task<List<MatchingOrder>> GetOrders(bool? isBid = null, string currencyPairId = null, int? count = Models.Constants.DefaultRequestOrdersCount,
            string userId = null, OrderStatusRequest status = OrderStatusRequest.Active,
            DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            var result = await _context.GetOrders(isBid, currencyPairId, count, userId, status, from, to);
            return result;
        }

        /// <summary>
        /// Get order by id
        /// </summary>
        /// <param name="id">order id</param>
        /// <param name="isBid"></param>
        /// <returns>Ask</returns>
        [HttpGet("{id}")]
        public async Task<MatchingOrder> GetOrder(Guid id, bool? isBid = null)
        {
            var order = await _context.GetOrder(id, isBid);
            return order;
        }

        /// <summary>
        /// Create Order
        /// </summary>
        /// <param name="request">Order details</param>
        /// <returns>New Order Id</returns>
        [ProducesResponseType(typeof(OrderCreateResult), 200)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] OrderCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newOrderId = await _tradingService.CreateOrder(request);
            return Ok(new OrderCreateResult { Id = newOrderId });
        }

        /// <summary>
        /// Delete Order by id
        /// </summary>
        /// <param name="orderId">Order id</param>
        /// <param name="userId">User id</param>
        /// <returns></returns>
        [HttpDelete("{orderId}")]
        [HttpDelete("{isBid}/{orderId}")] // Obsolete
        public async Task<CancelOrderResponse> Delete(Guid orderId)
        {
            var response = await _tradingService.CancelOrder(orderId);
            return response;
        }

        [HttpPost("send-active-to-marketdata")]
        public async Task<IActionResult> SendActiveOrdersToMarketData()
        {
            await _matchingPoolsHandler.SendActiveOrdersToMarketData();
            return Ok();
        }

        [HttpPost("resend-to-marketdata")]
        public async Task<IActionResult> ResendOrdersToMarketData(DateTimeOffset? from = null)
        {
            if (from == null)
                from = DateTimeOffset.MinValue;

            int page = 0, pageSize = 1000;
            while (true)
            {
                var orders = (await _context.Bids.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot && _.DateCreated > from)
                    .Skip(page++ * pageSize).Take(pageSize)
                    .ToListAsync())
                    .Cast<MatchingOrder>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"SendAllOrdersToMarketData() bids, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await _marketDataService.SendOldOrders(orders);
            }
            page = 0;
            while (true)
            {
                var orders = (await _context.Asks.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot && _.DateCreated > from)
                    .Skip(page++ * pageSize).Take(pageSize)
                    .ToListAsync())
                    .Cast<MatchingOrder>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"SendAllOrdersToMarketData() asks, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await _marketDataService.SendOldOrders(orders);
            }
            return Ok();
        }

        [HttpPost("old-orders")]
        public async Task<IActionResult> SaveOldOrders([FromBody] List<Order> orders)
        {
            var bids = orders.Where(_ => _.IsBid).ToList();
            var bidIds = bids.Select(_ => _.Id).ToList();
            var dbBidIds = (await _context.Bids.Where(_ => bidIds.Contains(_.Id)).ToListAsync())
                .Select(_ => _.Id).ToList();
            var bidsToSave = bids.Where(_ => !dbBidIds.Contains(_.Id)).ToList();

            var asks = orders.Where(_ => !_.IsBid).ToList();
            var askIds = asks.Select(_ => _.Id).ToList();
            var dbAskIds = (await _context.Asks.Where(_ => askIds.Contains(_.Id)).ToListAsync())
                .Select(_ => _.Id).ToList();
            var asksToSave = asks.Where(_ => !dbAskIds.Contains(_.Id)).ToList();

            _logger.LogInformation($"SaveOldOrders Bids came:{bids.Count} new:{bidsToSave.Count}. " +
                $"Asks came:{asks.Count} new:{asksToSave.Count}");
            _context.AddRange(bidsToSave);
            _context.AddRange(asksToSave);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("ids")]
        public async Task<IEnumerable<Guid>> GetAllOrderIds()
        {
            var bidIds = await _context.Bids.Where(_ => _.ClientType != ClientType.DealsBot)
                .Select(_ => _.Id).ToListAsync();
            var askIds = await _context.Asks.Where(_ => _.ClientType != ClientType.DealsBot)
                .Select(_ => _.Id).ToListAsync();
            return bidIds.Union(askIds);
        }

        [HttpPost("recancel-in-depository")]
        public async Task<IActionResult> RecancelAllOrdersInDepository(DateTimeOffset? from = null,
            DateTimeOffset? to = null, bool editCancelAmounts = false)
        {
            int page = 0, pageSize = 1000;
            var request = "depository/deal/check-canceled-orders".InternalApi()
                .SetQueryParam(nameof(editCancelAmounts), editCancelAmounts);
            while (true)
            {
                var orders = (await _context.Bids.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize)
                    .ToListAsync())
                    .Cast<MatchingOrder>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"RecancelAllOrdersInDepository() bids, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await request.PostJsonAsync(orders);
            }
            page = 0;
            while (true)
            {
                var orders = (await _context.Asks.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize)
                    .ToListAsync())
                    .Cast<MatchingOrder>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"RecancelAllOrdersInDepository() asks, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await request.PostJsonAsync(orders);
            }
            return Ok();
        }

        [HttpPost("fix-fullfilled")]
        public async Task<IActionResult> FixOrdersFullfilled(DateTimeOffset? from = null,
            DateTimeOffset? to = null, bool editFullfilled = false)
        {
            int page = 0, pageSize = 10000;
            while (true)
            {
                var orders = await _context.Bids.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize)
                    .Include(_ => _.DealList).ToListAsync();
                if (orders.Count == 0)
                    break;

                _logger.LogInformation($"FixOrdersFullfilled() bids, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                foreach (var order in orders)
                {
                    decimal dealsFullfilled = order.DealList.Select(_ => _.Volume).DefaultIfEmpty(0).Sum();
                    if (order.Fulfilled == dealsFullfilled)
                        continue;
                    _logger.LogWarning($"FixOrdersFullfilled() Fulfilled needs to change:" +
                        $"{order.Fulfilled} -> {dealsFullfilled} for {order}");
                    if (editFullfilled)
                        order.Fulfilled = dealsFullfilled;
                }
                if (editFullfilled)
                    await _context.SaveChangesAsync();
            }
            page = 0;
            while (true)
            {
                var orders = await _context.Asks.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize)
                    .Include(_ => _.DealList).ToListAsync();
                if (orders.Count == 0)
                    break;

                _logger.LogInformation($"FixOrdersFullfilled() asks, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                foreach (var order in orders)
                {
                    decimal dealsFullfilled = order.DealList.Select(_ => _.Volume).DefaultIfEmpty(0).Sum();
                    if (order.Fulfilled == dealsFullfilled)
                        continue;
                    _logger.LogWarning($"FixOrdersFullfilled() Fulfilled needs to change:" +
                        $"{order.Fulfilled} -> {dealsFullfilled} for {order}");
                    if (editFullfilled)
                        order.Fulfilled = dealsFullfilled;
                }
                if (editFullfilled)
                    await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}

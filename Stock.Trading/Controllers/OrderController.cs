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
        public async Task<List<Order>> GetOrders(bool? isBid = null, string currencyPairId = null, int? count = Constants.DefaultRequestOrdersCount,
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
        public async Task<Order> GetOrder(Guid id, bool? isBid = null)
        {
            var order = await _context.GetOrder(id, isBid);
            return order;
        }

        /// <summary>
        /// Create Order
        /// </summary>
        /// <param name="request">Order details</param>
        /// <returns>New Order Id</returns>
        [ProducesResponseType(typeof(CreateOrderResult), 200)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] OrderCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newOrderId = await _tradingService.CreateOrder(request);
            return Ok(new CreateOrderResult { Id = newOrderId });
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
            var response = await _tradingService.DeleteOrder(orderId);
            return response;
        }

        [HttpPost("send-active-to-marketdata")]
        public async Task<IActionResult> SendActiveOrdersToMarketData()
        {
            await _matchingPoolsHandler.SendActiveOrdersToMarketData();
            return Ok();
        }

        [HttpPost("resend-to-marketdata")]
        public async Task<IActionResult> ResendAllOrdersToMarketData(DateTimeOffset? from = null)
        {
            if (from == null)
                from = DateTimeOffset.MinValue;

            int page = 0, pageSize = 1000;
            while (true)
            {
                var orders = (await _context.Bids.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.ClientType != ClientType.DealsBot && _.DateCreated > from)
                    .Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
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
                    .Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"SendAllOrdersToMarketData() asks, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await _marketDataService.SendOldOrders(orders);
            }
            return Ok();
        }

        [HttpPost("recancel-in-depository")]
        public async Task<IActionResult> RecancelAllOrdersInDepository(DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            int page = 0, pageSize = 1000;
            string url = "depository/deal/check-canceled-orders";
            while (true)
            {
                var orders = (await _context.Bids.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.IsCanceled && _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"RecancelAllOrdersInDepository() bids, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await _gatewayHttpClient.PostJsonAsync(url, orders);
            }
            page = 0;
            while (true)
            {
                var orders = (await _context.Asks.OrderBy(_ => _.DateCreated)
                    .Where(_ => _.IsCanceled && _.ClientType != ClientType.DealsBot
                        && (from == null || _.DateCreated > from) && (to == null || _.DateCreated < to))
                    .Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"RecancelAllOrdersInDepository() asks, page {page}, count:{orders.Count}, " +
                    $"firstDate:{orders.First().DateCreated:o}");
                await _gatewayHttpClient.PostJsonAsync(url, orders);
            }
            return Ok();
        }
    }
}

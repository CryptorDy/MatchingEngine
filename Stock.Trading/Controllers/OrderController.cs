using MatchingEngine.Data;
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
        private readonly TradingService _service;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger _logger;

        public OrderController(TradingDbContext context,
            TradingService service,
            MarketDataService marketDataService,
            ILogger<OrderController> logger)
        {
            _context = context;
            _service = service;
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
            var order = await _service.GetOrder(id, isBid);
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

            var newOrderId = await _service.CreateOrder(request);
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
            var response = await _service.DeleteOrder(orderId);
            return response;
        }

        [HttpPost("send-all-to-marketdata")]
        public async Task<IActionResult> SendAllOrdersToMarketData()
        {
            int page = 0, pageSize = 1000;
            //while (true)
            //{
            //    var orders = (await _context.Bids.OrderBy(_ => _.DateCreated).Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
            //    if (orders.Count == 0)
            //        break;
            //    _logger.LogInformation($"SendAllOrdersToMarketData() bids, page {page}");
            //    await _marketDataService.SendOldOrders(orders);
            //}
            page = 0;
            while (true)
            {
                var orders = (await _context.Asks.OrderBy(_ => _.DateCreated).Skip(page++ * pageSize).Take(pageSize).ToListAsync()).Cast<Order>().ToList();
                if (orders.Count == 0)
                    break;
                _logger.LogInformation($"SendAllOrdersToMarketData() asks, page {page}");
                await _marketDataService.SendOldOrders(orders);
            }
            return Ok();
        }
    }
}

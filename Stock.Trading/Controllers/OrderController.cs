using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatchingEngine.Controllers
{
    [Route("api/[controller]")]
    public class OrderController : Controller
    {
        private readonly TradingDbContext _context;
        private readonly TradingService _service;

        public OrderController(TradingDbContext context,
            TradingService service)
        {
            _context = context;
            _service = service;
        }

        /// <summary>
        /// Get orders list
        /// </summary>
        [HttpGet("orders/{isBid}/{userId?}")]
        public async Task<List<Order>> GetOrders(bool isBid, string userId = null)
        {
            var result = await _context.GetOrders(isBid, userId);
            return result;
        }

        /// <summary>
        /// Get orders list
        /// </summary>
        [HttpGet("active-orders")]
        public async Task<List<Order>> GetActiveOrders()
        {
            var result = await _context.GetOrders(onlyActive: true);
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
            var order = await _service.GetOrder(isBid, id);
            return order;
        }

        /// <summary>
        /// Create Order
        /// </summary>
        /// <param name="request">Order details</param>
        /// <returns>New Order Id</returns>
        [ProducesResponseType(typeof(CreateOrderResult), 200)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]OrderCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.CurrencyPairCode == "XSP_BTC") Console.WriteLine($"AddOrder {request.CurrencyPairCode} {request.ActionId} {DateTime.Now.ToString("hh:mm:ss.fff")} init");
            var newOrderId = await _service.CreateOrder(request);
            return Ok(new CreateOrderResult { Id = newOrderId });
        }

        /// <summary>
        /// Delete Order by id
        /// </summary>
        /// <param name="isBid">Order isBid</param>
        /// <param name="id">Order id</param>
        /// <param name="userId">User id</param>
        /// <returns></returns>
        [HttpDelete("{isBid}/{id}")]
        public async Task<CancelOrderResponse> Delete(bool isBid, Guid id, string userId)
        {
            var response = await _service.DeleteOrder(isBid, id, userId);
            return response;
        }
    }
}

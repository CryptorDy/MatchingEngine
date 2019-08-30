using MatchingEngine.Models;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatchingEngine.Controllers
{
    [Route("api/[controller]")]
    public class OrderController : Controller
    {
        private readonly TradingService _service;

        public OrderController(TradingService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get orders list
        /// </summary>
        [HttpGet("orders/{isBid}/{userId?}")]
        public async Task<List<Order>> GetOrders(bool isBid, string userId = null)
        {
            var result = await _service.GetOrders(isBid, userId);
            return result;
        }

        /// <summary>
        /// Get order by id
        /// </summary>
        /// <param name="id">order id</param>
        /// <returns>Ask</returns>
        [HttpGet("{isBid}/{id}")]
        public async Task<Order> GetOrder(bool isBid, string id)
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
                return BadRequest(ModelState);

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
        public async Task<IActionResult> Delete(bool isBid, string id, string userId)
        {
            int res = await _service.DeleteOrder(isBid, id, userId);
            if (res != 0)
                return Ok();
            return NotFound();
        }
    }
}
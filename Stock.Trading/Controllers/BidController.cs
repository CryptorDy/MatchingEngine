using Microsoft.AspNetCore.Mvc;
using Stock.Trading.Entities;
using Stock.Trading.Requests;
using Stock.Trading.Responses;
using Stock.Trading.Service;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class BidController : Controller
    {
        private readonly TradingService _service;

        public BidController(TradingService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get bids list
        /// </summary>
        /// <returns></returns>
        [HttpGet("bids")]
        public async Task<List<Bid>> Bids()
        {
            return await _service.Bids();
        }

        /// <summary>
        /// Get bid by id
        /// </summary>
        /// <param name="id">Bid id</param>
        /// <returns>Bid details</returns>
        [HttpGet("{id}")]
        public async Task<Bid> Bid(string id)
        {
            return await _service.Bid(id);
        }

        /// <summary>
        /// Create bid
        /// </summary>
        /// <param name="request">Bid details</param>
        /// <returns>New bid Id</returns>
        [ProducesResponseType(typeof(CreateOrderResult), 200)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]AddRequest request)
        {
            var newId = await _service.CreateBid(request);

            return Ok(new CreateOrderResult { Id = newId});
        }

        /*
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody]Bid o)
        {
            if (id == o.Id)
            {
                int res = await Command.Get<BidCommand>().UpdateOrderAsync(id, o);
                if (res != 0)
                {
                    return Ok(res);
                }
                return NotFound();
            }
            return NotFound();
        }
        */

        /// <summary>
        /// Delete bid
        /// </summary>
        /// <param name="id">Bid id</param>
        /// <param name="userId">User id</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id, string userId)
        {
            int res = await _service.DeleteBidAsync(id, userId);

            if (res != 0)
            {
                return Ok();
            }

            return NotFound();
        }
    }
}

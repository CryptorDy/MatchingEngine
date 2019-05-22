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
    public class AskController : Controller
    {
        private readonly TradingService _service;

        public AskController(TradingService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get asks list
        /// </summary>
        /// <returns>Asks list</returns>
        [HttpGet("asks")]
        public async Task<List<Ask>> Asks(string userId = null)
        {
            return await _service.Asks(userId);
        }

        /// <summary>
        /// Get Ask by id
        /// </summary>
        /// <param name="id">Ask id</param>
        /// <returns>Ask</returns>
        [HttpGet("{id}")]
        public async Task<Ask> Get(string id)
        {
            var ask = await _service.Ask(id);
            return ask;
        }

        /// <summary>
        /// Create ask
        /// </summary>
        /// <param name="request">Ask details</param>
        /// <returns>New ask Id</returns>
        [ProducesResponseType(typeof(CreateOrderResult), 200)]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]AddRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var newId = await _service.CreateAsk(request);

            return Ok(new CreateOrderResult { Id = newId });
        }

        /// <summary>
        /// Delete ask by id
        /// </summary>
        /// <param name="id">Ask id</param>
        /// <param name="userId">User id</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id, string userId)
        {
            int res = await _service.DeleteAskAsync(id, userId);

            if (res != 0)
            {
                return Ok();
            }

            return NotFound();
        }
    }
}

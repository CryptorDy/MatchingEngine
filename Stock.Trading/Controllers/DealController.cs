using Microsoft.AspNetCore.Mvc;
using Stock.Trading.Entities;
using Stock.Trading.Responses;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class DealController : Controller
    {
        private readonly TradingService _service;

        public DealController(TradingService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get deal by id
        /// </summary>
        /// <param name="id">Deal id</param>
        [HttpGet("deal/{id}")]
        public Deal Deal(string id)
        {
            return _service.GetDeal(id);
        }

        /// <summary>
        /// Get DealResponse by id
        /// </summary>
        /// <param name="id">Deal id</param>
        /// <returns>Deal details</returns>
        [HttpGet("{id}")]
        public DealResponse Get(string id)
        {
            return _service.GetDealResponse(id);
        }

        /// <summary>
        /// Get all deals
        /// </summary>
        /// <param name="count">Count</param>
        /// <returns>Deals list</returns>
        [HttpGet("deals")]
        public async Task<List<Deal>> GetDeals(string currencyPairId = null, int? count = null, string userId = null, DateTime? sinceDate = null)
        {
            var result = await _service.GetDeals(currencyPairId, count, userId, sinceDate);
            return result;
        }
    }
}

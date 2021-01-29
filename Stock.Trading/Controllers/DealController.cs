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

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class DealController : Controller
    {
        private readonly TradingService _service;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger _logger;

        public DealController(
            TradingService service,
            MarketDataService marketDataService,
            ILogger<DealController> logger)
        {
            _service = service;
            _marketDataService = marketDataService;
            _logger = logger;
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
        /// <param name="currencyPairId"></param>
        /// <param name="count">Count</param>
        /// <param name="userId"></param>
        /// <param name="sinceDate"></param>
        /// <param name="toDate"></param>
        /// <param name="dealIds"></param>
        /// <returns>Deals list</returns>
        [HttpGet("deals")]
        public async Task<List<Deal>> GetDeals(string currencyPairId = null, int? count = null, string userId = null,
            DateTime? sinceDate = null, DateTimeOffset? toDate = null, List<string> dealIds = null)
        {
            var result = await _service.GetDeals(currencyPairId, count, userId, sinceDate, toDate, dealIds);
            return result;
        }

        [HttpPost("marketdata/resend")]
        public async Task<IActionResult> ResendDealsToMarketData(DateTimeOffset? from = null, int pageSize = 1000)
        {
            await _marketDataService.SendDealsFromDate(from, pageSize);
                    .Where(_ => from == null || _.DateCreated >= from).OrderBy(_ => _.DateCreated)
            return Ok();
        }


        [HttpPost("resave-from-marketdata")]
        public async Task<IActionResult> ResaveOldDeals([FromBody] List<DealResponse> dealResponses)
        {
            var dealIds = dealResponses.Select(_ => _.DealId).ToList();
            var dbDealIds = await _context.Deals.Where(_ => dealIds.Contains(_.DealId)).Select(_ => _.DealId).ToListAsync();
            var dbDealCopiesIds = await _context.DealCopies.Where(_ => dealIds.Contains(_.DealId)).Select(_ => _.DealId).ToListAsync();
            int addedCounter = 0;
            foreach (var dealResponse in dealResponses)
            {
                if (dbDealIds.Contains(dealResponse.DealId))
                    continue;
                var deal = new Deal
                {
                    DealId = dealResponse.DealId,
                    DateCreated = new DateTimeOffset(dealResponse.DealDateUtc, TimeSpan.Zero),
                    Volume = dealResponse.Volume,
                    Price = dealResponse.Price,
                    BidId = dealResponse.BidId,
                    AskId = dealResponse.AskId,
                    FromInnerTradingBot = dealResponse.FromInnerTradingBot,
                    IsSentToDealEnding = dealResponse.FromInnerTradingBot, // resend if not from bot
                };
                _context.Deals.Add(deal);
                if (!dbDealCopiesIds.Contains(dealResponse.DealId))
                    _context.DealCopies.Add(new DealCopy(deal));
                addedCounter++;
            }
            _logger.LogInformation($"ResaveOldDeals() added {addedCounter}, dealResponses:{dealResponses.Count}, dbDealIds:{dbDealIds.Count}");
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

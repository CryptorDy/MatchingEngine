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
using TLabs.ExchangeSdk.Trading;

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class DealController : Controller
    {
        private readonly TradingDbContext _context;
        private readonly TradingService _service;
        private readonly MarketDataService _marketDataService;
        private readonly DealDeleteService _dealDeleteService;
        private readonly ILogger _logger;

        public DealController(TradingDbContext context,
            TradingService service,
            MarketDataService marketDataService,
            DealDeleteService dealDeleteService,
            ILogger<DealController> logger)
        {
            _context = context;
            _service = service;
            _marketDataService = marketDataService;
            _dealDeleteService = dealDeleteService;
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
        public MarketdataDeal Get(string id)
        {
            return _service.GetDealResponse(id);
        }

        /// <summary>Get deals</summary>
        /// <returns>Deals list</returns>
        [HttpGet("deals")]
        public async Task<List<Deal>> GetDeals(string currencyPairId = null, int? count = null, string userId = null,
            DateTime? sinceDate = null, DateTimeOffset? toDate = null, List<string> dealIds = null)
        {
            var result = await _service.GetDeals(currencyPairId, count, new List<string> { userId }, sinceDate, toDate, dealIds);
            return result;
        }

        /// <summary>Get deal responses for list of users</summary>
        /// <returns>Deals list</returns>
        [HttpPost("responses")]
        public async Task<List<MarketdataDeal>> GetDealResponses([FromBody] List<string> userIds,
            string currencyPairId = null, int? count = null,
            DateTime? sinceDate = null, DateTimeOffset? toDate = null)
        {
            var deals = await _service.GetDeals(currencyPairId, count, userIds, sinceDate, toDate);
            var responses = deals.Select(_ => _.GetDealResponse()).ToList();
            return responses;
        }

        [HttpPost("marketdata/resend")]
        public async Task<IActionResult> ResendDealsToMarketData(DateTimeOffset from, int pageSize = 1000)
        {
            await _marketDataService.SendDealsFromDate(from, pageSize);
            return Ok();
        }


        [HttpPost("resave-from-marketdata")]
        public async Task<IActionResult> ResaveOldDeals([FromBody] List<MarketdataDeal> dealResponses)
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
            _logger.LogInformation($"ResaveOldDeals() added {addedCounter}, dealResponses:{dealResponses.Count}, dbDealIds:{dbDealIds.Count}. " +
                $"Date:{dealResponses?.First()?.DealDateUtc:o}");
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("")]
        public async Task<IActionResult> DeleteDeals(string currencyPairCode, DateTimeOffset from, DateTimeOffset to)
        {
            await _dealDeleteService.DeleteDeals(currencyPairCode, from, to);
            return Ok();
        }
    }
}

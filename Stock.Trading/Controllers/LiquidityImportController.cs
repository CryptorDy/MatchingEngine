using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MatchingEngine.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MatchingEngine.Data;
using MatchingEngine.Services;
using MatchingEngine.Models;

namespace Stock.Trading.Controllers
{
    [Route("api/liquidityimport")]
    public class LiquidityImportController : Controller
    {
        private readonly TradingDbContext _context;
        private readonly TradingService _tradingService;
        private readonly MatchingPool _matchingPool;
        private readonly LiquidityExpireWatcher _liquidityExpireWatcher;
        private readonly ILogger _logger;

        public LiquidityImportController(TradingDbContext context,
            TradingService tradingService,
            MatchingPoolAccessor matchingPoolAccessor,
            LiquidityExpireWatcherAccessor liquidityExpireWatcherAccessor,
            ILogger<LiquidityImportController> logger)
        {
            _context = context;
            _tradingService = tradingService;
            _matchingPool = matchingPoolAccessor.MatchingPool;
            _liquidityExpireWatcher = liquidityExpireWatcherAccessor.LiquidityExpireWatcher;
            _logger = logger;
        }

        [HttpPost("trade-result")]
        public async Task<SaveExternalOrderResult> ApplyTradeResult([FromBody]ExternalCreatedOrder createdOrder)
        {
            var result = await _matchingPool.UpdateExternalOrder(createdOrder);
            return result;
        }

        /// <summary>
        /// Notify that liquidity import is working
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        [HttpGet("ping/{exchange}/{curPairCode}")]
        public async Task<IActionResult> Ping(Exchange exchange, string curPairCode)
        {
            _liquidityExpireWatcher.UpdateExpirationDate((int)exchange, curPairCode);
            return Ok();
        }

        [HttpPut("orders/update")]
        public async Task<IActionResult> UpdateOrders([FromBody]List<OrderCreateRequest> orders)
        {
            try
            {
                await _matchingPool.UpdateOrders(orders);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return Ok();
        }

        [HttpDelete("orders/{exchangeId}/{currencyPairId}")]
        public async Task<IActionResult> DeleteOrders(Exchange exchange, string currencyPairId)
        {
            _matchingPool.RemoveLiquidityOrderbook(exchange, currencyPairId);
            return Ok();
        }

        /// <summary>
        /// Delete previously imported orders
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        [HttpPost("orders/delete")]
        public async Task<IActionResult> DeleteOrders([FromBody]List<OrderCreateRequest> orders)
        {
            await _matchingPool.RemoveOrders(orders.Select(_ => Guid.Parse(_.ActionId)).ToList());
            return Ok();
        }

        /// <summary>
        /// Add imported orders
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        [HttpPost("orders/add")]
        public async Task<IActionResult> AddOrders([FromBody]List<OrderCreateRequest> orderRequests)
        {
            var orders = orderRequests.Select(_ => _.GetOrder()).ToList();
            orders.ForEach(_ => _matchingPool.AppendOrder(_));
            return Ok();
        }
    }
}

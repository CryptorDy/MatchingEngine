using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            SingletonsAccessor singletonsAccessor,
            SingletonsAccessor liquidityExpireWatcherAccessor,
            ILogger<LiquidityImportController> logger)
        {
            _context = context;
            _tradingService = tradingService;
            _matchingPool = singletonsAccessor.MatchingPool;
            _liquidityExpireWatcher = liquidityExpireWatcherAccessor.LiquidityExpireWatcher;
            _logger = logger;
        }

        [HttpPost("trade-result")]
        public async Task<SaveExternalOrderResult> ApplyTradeResult([FromBody]ExternalCreatedOrder createdOrder)
        {
            var result = await _matchingPool.UpdateExternalOrder(createdOrder);
            return result;
        }

        [HttpPost("orders-update")]
        public async Task SaveLiquidityImportUpdate([FromBody]ImportUpdateDto dto)
        {
            // check that there is no local orders
            var localOrders = dto.OrdersToAdd.Union(dto.OrdersToUpdate).Union(dto.OrdersToDelete)
                .Where(_ => _.Exchange == Exchange.Local).ToList();
            if (localOrders.Count > 0)
            {
                Console.WriteLine($"SaveLiquidityImportUpdate() has local orders: \n{string.Join("\n", localOrders.Select(_ => _.GetOrder()))}");
                throw new Exception($"SaveLiquidityImportUpdate() has local orders");
            }

            await _matchingPool.SaveLiquidityImportUpdate(dto);
        }

        /// <summary>
        /// Notify that liquidity import is working
        /// </summary>
        [HttpGet("ping/{exchange}/{curPairCode}")]
        public async Task<IActionResult> Ping(Exchange exchange, string curPairCode)
        {
            if (exchange == Exchange.Local)
            {
                return BadRequest();
            }

            _liquidityExpireWatcher.UpdateExpirationDate(exchange, curPairCode);
            return Ok();
        }

        [HttpDelete("orders/{exchange}/{currencyPairId}")]
        public async Task<IActionResult> DeleteOrders(Exchange exchange, string currencyPairId)
        {
            try
            {
                if (exchange == Exchange.Local)
                {
                    return BadRequest();
                }

                _matchingPool.RemoveLiquidityOrderbook(exchange, currencyPairId);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                return BadRequest();
            }
        }
    }
}

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
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly LiquidityExpireWatcher _liquidityExpireWatcher;
        private readonly ILogger _logger;

        public LiquidityImportController(
            SingletonsAccessor singletonsAccessor,
            ILogger<LiquidityImportController> logger)
        {
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _liquidityExpireWatcher = singletonsAccessor.LiquidityExpireWatcher;
            _logger = logger;
        }

        [HttpPost("trade-result")]
        public async Task<SaveExternalOrderResult> ApplyTradeResult([FromBody] ExternalCreatedOrder createdOrder)
        {
            var result = await _matchingPoolsHandler.GetPool(createdOrder.CurrencyPairCode)
                .UpdateExternalOrder(createdOrder);
            return result;
        }

        [HttpPost("orders-update")]
        public async Task SaveLiquidityImportUpdate([FromBody] List<ImportUpdateDto> dtos)
        {
            await Task.WhenAll(dtos.Select(async dto =>
            {
                // check that there is no local orders
                var localOrders = dto.OrdersToAdd.Union(dto.OrdersToUpdate).Union(dto.OrdersToDelete)
                    .Where(_ => _.Exchange == Exchange.Local).ToList();
                if (localOrders.Count > 0)
                {
                    Console.WriteLine($"SaveLiquidityImportUpdate() has local orders: \n" +
                        $"{string.Join("\n", localOrders.Select(_ => _.GetOrder()))}");
                    throw new Exception($"SaveLiquidityImportUpdate() has local orders");
                }

                _matchingPoolsHandler.GetPool(dto.CurrencyPairCode).SaveLiquidityImportUpdate(dto);
            }));
        }

        /// <summary>
        /// Notify that liquidity import is working
        /// </summary>
        [HttpGet("ping/{exchange}/{currencyPairCode}")]
        public async Task<IActionResult> Ping(Exchange exchange, string currencyPairCode)
        {
            if (exchange == Exchange.Local)
            {
                return BadRequest();
            }

            _liquidityExpireWatcher.UpdateExpirationDate(exchange, currencyPairCode);
            return Ok();
        }

        [HttpDelete("orders/{exchange}/{currencyPairCode}")]
        public async Task<IActionResult> DeleteOrders(Exchange exchange, string currencyPairCode)
        {
            try
            {
                if (exchange == Exchange.Local)
                {
                    return BadRequest();
                }

                _matchingPoolsHandler.GetPool(currencyPairCode).RemoveLiquidityOrderbook(exchange);
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

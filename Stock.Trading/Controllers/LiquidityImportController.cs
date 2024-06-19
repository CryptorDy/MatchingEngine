using MatchingEngine.Data;
using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Trading;

namespace Stock.Trading.Controllers
{
    [Route("api/liquidityimport")]
    public class LiquidityImportController : Controller
    {
        private readonly TradingDbContext _context;
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly LiquidityExpireWatcher _liquidityExpireWatcher;
        private readonly ILogger _logger;

        public LiquidityImportController(TradingDbContext context,
            SingletonsAccessor singletonsAccessor,
            ILogger<LiquidityImportController> logger)
        {
            _context = context;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _liquidityExpireWatcher = singletonsAccessor.LiquidityExpireWatcher;
            _logger = logger;
        }


        [HttpGet("trades/{id}")]
        public async Task<MatchingExternalTrade> GetExternalTrade(Guid id)
        {
            var model = await _context.ExternalTrades.FirstOrDefaultAsync(_ => _.Id == id);
            // Note: Bid and Ask models are not loaded
            return model;
        }

        [HttpPost("trades/result")]
        public async Task<IActionResult> EnqueueExternalTradeResult([FromBody] ExternalTrade externalTrade)
        {
            var orderId = externalTrade.IsBid ? externalTrade.TradingBidId : externalTrade.TradingAskId;
            _matchingPoolsHandler.GetPool(externalTrade.CurrencyPairCode)
                .EnqueuePoolAction(PoolActionType.ExternalTradeResult, orderId, externalTrade: externalTrade);
            return Ok();
        }

        [Obsolete("For LiquidityMain versions <= 1.0.291")]
        [HttpPost("orders-update")]
        public async Task SaveLiquidityImportUpdate([FromBody] ImportUpdateDto dto)
        {
            var groupsToAdd = dto.OrdersToAdd.GroupBy(_ => _.CurrencyPairCode).ToDictionary(_ => _.Key, _ => _.ToList());
            var groupsToUpdate = dto.OrdersToUpdate.GroupBy(_ => _.CurrencyPairCode).ToDictionary(_ => _.Key, _ => _.ToList());
            var groupsToDelete = dto.OrdersToDelete.GroupBy(_ => _.CurrencyPairCode).ToDictionary(_ => _.Key, _ => _.ToList());
            var pairCodes = groupsToAdd.Keys.ToList().Union(groupsToUpdate.Keys).Union(groupsToDelete.Keys)
                .Distinct().ToList();

            if (new Random().Next(50) == 0)
                _logger.LogInformation($"Used obsolete /orders-update with {pairCodes.Count} pairs");

            var separateDtos = new List<ImportUpdateDto>();
            foreach (var pairCode in pairCodes)
            {
                separateDtos.Add(new ImportUpdateDto
                {
                    CurrencyPairCode = pairCode,
                    OrdersToAdd = groupsToAdd.GetValueOrDefault(pairCode, new()),
                    OrdersToUpdate = groupsToUpdate.GetValueOrDefault(pairCode, new()),
                    OrdersToDelete = groupsToDelete.GetValueOrDefault(pairCode, new()),
                });
            }
            await SaveLiquidityImportUpdates(separateDtos);
        }

        [HttpPost("orders-updates")]
        public async Task SaveLiquidityImportUpdates([FromBody] List<ImportUpdateDto> dtos)
        {
            await Task.WhenAll(dtos.Select(async dto =>
            {
                // temporary check that there is no local orders
                if (dto.OrdersToAdd.Any(_ => _.Exchange == Exchange.Local) ||
                    dto.OrdersToUpdate.Any(_ => _.Exchange == Exchange.Local) ||
                    dto.OrdersToDelete.Any(_ => _.Exchange == Exchange.Local)
                )
                {
                    throw new Exception($"SaveLiquidityImportUpdate() has local orders");
                }

                _matchingPoolsHandler.GetPool(dto.CurrencyPairCode).SaveLiquidityImportedOrders(dto);
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

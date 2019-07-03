using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stock.Trading.Data;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using Stock.Trading.Models;
using Stock.Trading.Models.LiquidityImport;
using Stock.Trading.Requests;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        [HttpPut("order/{isBid}/{id}")]
        public async Task<IActionResult> UpdateOrder(bool isBid, string id, [FromBody]AddRequest order)
        {
            if (isBid)
            {
                var orderDb = await _context.Bids.SingleOrDefaultAsync(_ => _.Id.ToString() == id);
                if (orderDb == null)
                    return NotFound();
                if (orderDb.ExchangeId == 0)
                    return StatusCode((int)HttpStatusCode.MethodNotAllowed);
                if (orderDb.Fulfilled > order.Amount)
                    order.Amount = orderDb.Fulfilled;
                orderDb.Volume = order.Amount;
            }
            else
            {
                var orderDb = await _context.Asks.SingleOrDefaultAsync(_ => _.Id.ToString() == id);
                if (orderDb == null)
                    return NotFound();
                if (orderDb.ExchangeId == 0)
                    return StatusCode((int)HttpStatusCode.MethodNotAllowed);
                if (orderDb.Fulfilled > order.Amount)
                    order.Amount = orderDb.Fulfilled;
                orderDb.Volume = order.Amount;
            }

            await _matchingPool.UpdateOrder(Guid.Parse(id), order);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("orders/update")]
        public async Task<IActionResult> UpdateOrders([FromBody]List<AddRequestLiquidity> orders)
        {
            try
            {
                await _matchingPool.UpdateOrders(orders);

                //var bids = orders.Where(_ => _.IsBid).ToDictionary(x => x.TradingOrderId);
                //var bidsIds = bids.Keys.Select(Guid.Parse).ToList();

                //var bidsDb = await _context.Bids.Where(b => bidsIds.Contains(b.Id)).ToListAsync();
                //bidsDb.ForEach(orderDb =>
                //{
                //    if (orderDb.ExchangeId != 0)
                //    {
                //        var bid = bids[orderDb.Id.ToString()];
                //        if (orderDb.Fulfilled > bid.Amount)
                //            bid.Amount = orderDb.Fulfilled;
                //        orderDb.Volume = bid.Amount;
                //    }
                //});
                //await _matchingPool.UpdateOrders(bidsDb);

                //var asks = orders.Where(_ => !_.IsBid).ToDictionary(x => x.TradingOrderId);
                //var asksIds = asks.Keys.Select(Guid.Parse).ToList();

                //var asksDb = await _context.Asks.Where(b => asksIds.Contains(b.Id)).ToListAsync();
                //asksDb.ForEach(orderDb =>
                //{
                //    if (orderDb.ExchangeId != 0)
                //    {
                //        var ask = asks[orderDb.Id.ToString()];
                //        if (orderDb.Fulfilled > ask.Amount)
                //            ask.Amount = orderDb.Fulfilled;
                //        orderDb.Volume = ask.Amount;
                //    }
                //});
                //await _matchingPool.UpdateOrders(asksDb);
                //await _context.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return Ok();
        }

        [HttpDelete("orders/{exchangeId}/{currencyPairId}")]
        public async Task<IActionResult> DeleteOrders(int exchangeId, string currencyPairId)
        {
            _matchingPool.RemoveLiquidityOrderbook(exchangeId, currencyPairId);

            //var bids = await _context.Bids.Where(_ => _.ExchangeId == exchangeId && _.CurrencyPairId.ToLowerInvariant() == currencyPairId.ToLowerInvariant()).ToListAsync();
            // await _matchingPool.RemoveBids(bids.Select(_ => _.Id).ToList());

            //_context.Bids.RemoveRange(bids);

            //var asks = await _context.Asks.Where(_ => _.ExchangeId == exchangeId && _.CurrencyPairId == currencyPairId).ToListAsync();
            //await _matchingPool.RemoveAsks(asks.Select(_ => _.Id).ToList());

            //_context.Asks.RemoveRange(asks);

            //await _context.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Delete previously imported orders
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        [HttpPost("orders/delete")]
        public async Task<IActionResult> DeleteOrders([FromBody]List<Order> orders)
        {
            await _matchingPool.RemoveOrders(orders.Select(_ => Guid.Parse(_.TradingOrderId)).ToList());

            //asks.ForEach(_ =>
            //{
            //    _.OrderTypeCode = OrderType.Canceled.Code;
            //});

            //await _context.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Add imported orders
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        [HttpPost("orders/add")]
        public async Task<IActionResult> AddOrders([FromBody]List<AddRequestLiquidity> orders)
        {
            List<MBid> bids = new List<MBid>();
            var bidsDb = new List<Bid>();
            orders.Where(_ => _.IsBid).ToList().ForEach(_ =>
            {
                var bid = new Bid
                {
                    Id = Guid.Parse(_.TradingOrderId),
                    Volume = _.Amount,
                    Price = _.Price,
                    OrderDateUtc = _.OrderDateUtc,
                    CurrencyPairId = _.CurrencyPariId,
                    UserId = _.UserId,
                    ExchangeId = _.ExchangeId,
                    OrderTypeCode = OrderType.Active.Code
                };
                bidsDb.Add(bid);
                bids.Add(new MBid()
                {
                    Id = bid.Id,
                    UserId = bid.UserId,
                    Volume = bid.Volume,
                    Fulfilled = bid.Fulfilled,
                    Price = bid.Price,
                    Created = bid.OrderDateUtc,
                    CurrencyPairId = bid.CurrencyPairId,
                    ExchangeId = _.ExchangeId,
                    Status = MStatus.Active
                });
            });
            //await _context.Bids.AddRangeAsync(bidsDb);
            //await _context.SaveChangesAsync();
            bids.ForEach(_ => _matchingPool.AppendBid(_));

            List<MAsk> asks = new List<MAsk>();
            var asksDb = new List<Ask>();
            orders.Where(_ => !_.IsBid).ToList().ForEach(_ =>
            {
                var ask = new Ask
                {
                    Id = Guid.Parse(_.TradingOrderId),
                    Volume = _.Amount,
                    Price = _.Price,
                    OrderDateUtc = _.OrderDateUtc,
                    CurrencyPairId = _.CurrencyPariId, //todo: fixme
                    UserId = _.UserId,
                    ExchangeId = _.ExchangeId,
                    OrderTypeCode = OrderType.Active.Code
                };
                asksDb.Add(ask);
                asks.Add(new MAsk()
                {
                    Id = ask.Id,
                    UserId = ask.UserId,
                    Volume = ask.Volume,
                    Fulfilled = ask.Fulfilled,
                    Price = ask.Price,
                    Created = ask.OrderDateUtc,
                    CurrencyPairId = ask.CurrencyPairId,
                    ExchangeId = _.ExchangeId,
                    Status = MStatus.Active
                });
            });
            //await _context.Asks.AddRangeAsync(asksDb);
            //await _context.SaveChangesAsync();
            asks.ForEach(_ => _matchingPool.AppendAsk(_));

            var result = bidsDb.Select(x => new AddRequestLiquidity()
            {
                Amount = x.Volume,
                CurrencyPariId = x.CurrencyPairId,
                ExchangeId = x.ExchangeId,
                TradingOrderId = x.Id.ToString(),
                IsBid = true,
                OrderDateUtc = x.OrderDateUtc,
                Price = x.Price,
                UserId = x.UserId
            }).ToList();
            result.AddRange(asksDb.Select(x => new AddRequestLiquidity()
            {
                Amount = x.Volume,
                CurrencyPariId = x.CurrencyPairId,
                ExchangeId = x.ExchangeId,
                TradingOrderId = x.Id.ToString(),
                IsBid = false,
                OrderDateUtc = x.OrderDateUtc,
                Price = x.Price,
                UserId = x.UserId
            }));
            return Json(result);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stock.Trading.Data;
using Stock.Trading.Models.InnerTradingBot;
using Stock.Trading.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Controllers
{
    [Route("api/innertradingbot")]
    public class InnerTradingBotController
    {
        private readonly TradingDbContext _context;
        private readonly TradingService _tradingService;
        private readonly MatchingPool _matchingPool;
        private readonly ILogger _logger;

        public InnerTradingBotController(TradingDbContext context,
            TradingService tradingService,
            MatchingPoolAccessor matchingPoolAccessor,
            ILogger<InnerTradingBotController> logger)
        {
            _context = context;
            _tradingService = tradingService;
            _matchingPool = matchingPoolAccessor.MatchingPool;
            _logger = logger;
        }

        [HttpGet("currency-pairs-prices/{currencyPairsStr}")]
        public async Task<List<CurrencyPairPrices>> GetCurrencyPairPrices(string currencyPairsStr)
        {
            List<string> currecyPairCodes = currencyPairsStr.Split(new char[] { ',' }).ToList();
            var result = currecyPairCodes.Select(_ => _matchingPool.GetCurrencyPairPrices(_)).ToList();
            return result;
        }
    }
}

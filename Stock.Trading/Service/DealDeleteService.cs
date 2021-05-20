using Flurl.Http;
using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Models.Brokerage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    public class DealDeleteService
    {
        private readonly TradingDbContext _context;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly TradingService _tradingService;
        private readonly MatchingPoolsHandler _matchingPoolsHandler;
        private readonly MarketDataService _marketDataService;
        private readonly ILogger _logger;

        public DealDeleteService(TradingDbContext context,
            GatewayHttpClient gatewayHttpClient,
            TradingService tradingService,
            SingletonsAccessor singletonsAccessor,
            MarketDataService marketDataService,
            ILogger<DealDeleteService> logger)
        {
            _context = context;
            _gatewayHttpClient = gatewayHttpClient;
            _tradingService = tradingService;
            _matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public async Task DeleteDeals(string currencyPairCode, DateTimeOffset from, DateTimeOffset to)
        {
            _logger.LogWarning($"DeleteDeals currencyPair:{currencyPairCode}, from:{from:s}, to:{to:s}");

            var deals = await _context.Deals.Where(_ => _.Bid.CurrencyPairCode == currencyPairCode
               && _.DateCreated >= from && _.DateCreated < to)
               .Include(m => m.Bid).Include(m => m.Ask)
               .OrderBy(_ => _.DateCreated)
               .ToListAsync();
            var dealIds = deals.Select(_ => _.DealId).ToList();

            // find bids and asks of deleted deals
            var bids = deals.Select(_ => _.Bid).GroupBy(_ => _.Id).Select(_ => _.First()).ToList();
            var asks = deals.Select(_ => _.Ask).GroupBy(_ => _.Id).Select(_ => _.First()).ToList();

            // pay all users whose orders were in deleted deals
            var userIds = bids.Select(_ => _.UserId).Union(asks.Select(_ => _.UserId)).Distinct().ToList();
            userIds = userIds.Where(id => Guid.TryParse(id, out _)).ToList(); // remove bots

            _logger.LogWarning($"DeleteDeals deals:{deals.Count}, lastDealDate:{deals.Last()?.DateCreated}, " +
                $"bids:{bids.Count}, asks:{asks.Count}, userIds:{userIds.Count}");

            //await SendAirdrops(userIds); // Already sent

            // Delete deal transactions
            var dealTxActionIds = dealIds.SelectMany(_ =>
                new List<string> { _.ToString(), _.ToString() + "_ask" }).ToList();
            //await DepositoryDeleteTxsByActionIds(dealTxActionIds); // Already sent

            var orderIds = bids.Select(_ => _.Id.ToString())
                .Union(asks.Select(_ => _.Id.ToString())).ToList();
            //await DepositoryDeleteTxsByActionIds(orderIds); // Already sent

            // for each bid update depository Blocking txs, then update Amount & Fullfilled
            foreach (var order in bids)
            {
                decimal amountToRemove = deals.Where(_ => _.BidId == order.Id)
                    .Sum(_ => _.Volume).RoundDown(CurrenciesCache.Digits);
                decimal newAmount = order.Amount - amountToRemove;
                _logger.LogInformation($"DeleteDeals bid {order.Id} amount: {order.Amount} -> {newAmount}");

                // TODO Matching edit orders
                //bid.Fulfilled -= amountToRemove;
                //bid.Amount -= amountToRemove;

            }
            foreach (var order in asks)
            {
                decimal amountToRemove = deals.Where(_ => _.AskId == order.Id)
                    .Sum(_ => _.Volume).RoundDown(CurrenciesCache.Digits);
                decimal newAmount = order.Amount - amountToRemove;
                _logger.LogInformation($"DeleteDeals ask {order.Id} amount: {order.Amount} -> {newAmount}");

                // TODO Matching edit orders
                //bid.Fulfilled -= amountToRemove;
                //bid.Amount -= amountToRemove;
            }

            // TODO Depository send odrers changes

            //_context.Deals.RemoveRange(deals);
            //_context.DealCopies.RemoveRange(await context.DealCopies
            //    .Where(_ => dealIds.Contains(_.DealId)).ToListAsync());
            //await _context.SaveChangesAsync();

            // TODO Marketdata send orders/deals changes

        }

        private async Task SendAirdrops(List<string> userIds)
        {
            await SendAirdrop(new Airdrop
            {
                Id = "500000",
                CurrencyCode = "C3",
                Amount = 100,
                UserIds = userIds,
            });
            await SendAirdrop(new Airdrop
            {
                Id = "500001",
                CurrencyCode = "OTON",
                Amount = 100,
                UserIds = userIds,
            });
        }

        private async Task SendAirdrop(Airdrop airdrop)
        {
            _logger.LogInformation($"SendAirdrop() start {airdrop}");
            var response = await "cashrefill/crypto/airdrop-refill".InternalApi()
                .PostJsonAsync<string>(airdrop);
            _logger.LogInformation($"SendAirdrop() response:{response}");
        }

        private async Task DepositoryDeleteTxsByActionIds(List<string> actionIds)
        {
            var response = await "depository/transaction/delete".InternalApi()
                .PostJsonAsync(actionIds);
            _logger.LogInformation($"DepositoryDeleteTxsByActionIds() response:{response}");
        }
    }
}

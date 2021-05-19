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
    public interface IDealEndingService
    {
        Task SendDeal(Deal deal);

        Task SendDeals();

        Task DeleteDeals(string currencyPairCode, DateTimeOffset from, DateTimeOffset to);
    }

    public class DealEndingService : IDealEndingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        private const int _batchSize = 100;
        private static bool _isSending = false;

        public DealEndingService(
            IServiceScopeFactory serviceScopeFactory,
            GatewayHttpClient gatewayHttpClient,
            ILogger<DealEndingService> logger)
        {
            _scopeFactory = serviceScopeFactory;
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task SendDeal(Deal deal)
        {
            await _gatewayHttpClient.PostJsonAsync($"dealending/deal", deal);
        }

        public async Task SendDeals()
        {
            if (_isSending)
                return;
            try
            {
                _isSending = true;
                while (true)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                        var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                            .Where(_ => !_.IsSentToDealEnding && !_.FromInnerTradingBot)
                            .OrderByDescending(_ => _.DateCreated)
                            .Take(_batchSize)
                            .ToListAsync();
                        if (unprocessedDeals.Count == 0)
                            break;
                        _logger.LogInformation($"SendDeals() unprocessed:{unprocessedDeals.Count}");

                        int errorsCount = 0;
                        foreach (var deal in unprocessedDeals)
                        {
                            try
                            {
                                await SendDeal(deal);
                                deal.IsSentToDealEnding = true;
                                await context.SaveChangesAsync();
                                _logger.LogInformation($"SendDeals() sent: {deal.DealId}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                                errorsCount++;
                            }
                        }
                        _logger.LogInformation($"SendDeals() end. processed:{unprocessedDeals.Count}, with errors: {errorsCount}");
                    }
                    await Task.Delay(200);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
            _isSending = false;
        }


        public async Task DeleteDeals(string currencyPairCode, DateTimeOffset from, DateTimeOffset to)
        {
            _logger.LogWarning($"DeleteDeals currencyPair:{currencyPairCode}, from:{from:s}, to:{to:s}");

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

            var deals = await context.Deals.Where(_ => _.Bid.CurrencyPairCode == currencyPairCode
               && _.DateCreated >= from && _.DateCreated < to)
               .Include(m => m.Bid).Include(m => m.Ask)
               .OrderBy(_ => _.DateCreated)
               .ToListAsync();

            // find bids and asks of deleted deals
            var bids = deals.Select(_ => _.Bid).GroupBy(_ => _.Id).Select(_ => _.First()).ToList();
            var asks = deals.Select(_ => _.Ask).GroupBy(_ => _.Id).Select(_ => _.First()).ToList();

            // pay all users whose orders were in deleted deals
            var userIds = bids.Select(_ => _.UserId).Union(asks.Select(_ => _.UserId)).Distinct().ToList();
            userIds = userIds.Where(id => Guid.TryParse(id, out _)).ToList(); // remove bots

            _logger.LogWarning($"DeleteDeals deals:{deals.Count}, lastDealDate:{deals.Last()?.DateCreated}, " +
                $"bids:{bids.Count}, asks:{asks.Count}, userIds:{userIds.Count}");

            //await SendAirdrops(userIds); // already sent

            // Delete deal transactions
            var dealTxActionIds = deals.SelectMany(_ =>
                new List<string> { _.DealId.ToString(), _.DealId.ToString() + "_ask" }).ToList();
            await DepositoryDeleteTxsByActionIds(dealTxActionIds);

            // for each bid update depository Blocking txs, then update Amount & Fullfilled
            foreach (var order in bids)
            {
                decimal amountToRemove = deals.Where(_ => _.BidId == order.Id)
                    .Sum(_ => _.Volume).RoundDown(CurrenciesCache.Digits);
                decimal newAmount = order.Amount - amountToRemove;
                _logger.LogInformation($"DeleteDeals bid {order.Id} amount: {order.Amount} -> {newAmount}");
                // TODO Depository set newAmount to OrderingBegin, OrderingEnd txs

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
                // TODO Depository set newAmount to OrderingBegin, OrderingEnd txs

                // TODO Matching edit orders
                //bid.Fulfilled -= amountToRemove;
                //bid.Amount -= amountToRemove;
            }

            // TODO Marketdata send order updates

            //context.Deals.RemoveRange(deals);

            // TODO Marketdata delete deals

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

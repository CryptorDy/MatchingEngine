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
using TLabs.ExchangeSdk.Depository;

namespace MatchingEngine.Services
{
    public class DealDeleteService
    {
        private readonly TradingDbContext _context;
        private readonly TradingService _tradingService;
        private readonly MarketDataService _marketDataService;
        private readonly DepositoryClient _depositoryClient;
        private readonly ILogger _logger;

        public DealDeleteService(TradingDbContext context,
            TradingService tradingService,
            MarketDataService marketDataService,
            DepositoryClient depositoryClient,
            ILogger<DealDeleteService> logger)
        {
            _context = context;
            _tradingService = tradingService;
            _marketDataService = marketDataService;
            _depositoryClient = depositoryClient;
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
            var bidIds = deals.Select(_ => _.Bid).GroupBy(_ => _.Id).Select(_ => _.Key).ToList();
            var askIds = deals.Select(_ => _.Ask).GroupBy(_ => _.Id).Select(_ => _.Key).ToList();
            var bids = await _context.Bids.Where(_ => bidIds.Contains(_.Id)).ToListAsync();
            var asks = await _context.Asks.Where(_ => askIds.Contains(_.Id)).ToListAsync();

            // pay all users whose orders were in deleted deals
            var userIds = bids.Select(_ => _.UserId).Union(asks.Select(_ => _.UserId)).Distinct().ToList();
            userIds = userIds.Where(id => Guid.TryParse(id, out _)).ToList(); // remove bots
            //await SendAirdrops(userIds); // Already sent

            _logger.LogWarning($"DeleteDeals deals:{deals.Count}, lastDealDate:{deals.Last()?.DateCreated}, " +
                $"bids:{bids.Count}, asks:{asks.Count}, userIds:{userIds.Count}");

            // Delete deal transactions
            var dealTxActionIds = dealIds.SelectMany(_ =>
                new List<string> { _.ToString(), _.ToString() + "_ask" }).ToList();
            //await DepositoryDeleteTxsByActionIds(dealTxActionIds); // Already sent

            var orderIds = bids.Select(_ => _.Id.ToString())
                .Union(asks.Select(_ => _.Id.ToString())).ToList();
            //await DepositoryDeleteTxsByActionIds(orderIds); // Already sent

            List<Order> emptyOrders = new(), notEmptyOrders = new();
            // for each order update Amount & Fullfilled
            foreach (var order in bids)
            {
                decimal amountToRemove = deals.Where(_ => _.BidId == order.Id)
                    .Sum(_ => _.Volume).RoundDown(CurrenciesCache.Digits);
                decimal newAmount = order.Amount - amountToRemove;
                _logger.LogInformation($"DeleteDeals bid {order.Id} amount: {order.Amount} -> {newAmount}");

                order.Fulfilled -= amountToRemove;
                order.Amount -= amountToRemove;

                if (newAmount < 0)
                    throw new Exception($"Negative amount");
                else if (newAmount == 0)
                {
                    emptyOrders.Add(order);
                    _context.Bids.Remove(order);
                }
                else
                {
                    notEmptyOrders.Add(order);
                }
            }
            foreach (var order in asks)
            {
                decimal amountToRemove = deals.Where(_ => _.AskId == order.Id)
                    .Sum(_ => _.Volume).RoundDown(CurrenciesCache.Digits);
                decimal newAmount = order.Amount - amountToRemove;
                _logger.LogInformation($"DeleteDeals ask {order.Id} amount: {order.Amount} -> {newAmount}");

                order.Fulfilled -= amountToRemove;
                order.Amount -= amountToRemove;

                if (newAmount < 0)
                    throw new Exception($"Negative amount");
                else if (newAmount == 0)
                {
                    emptyOrders.Add(order);
                    _context.Asks.Remove(order);
                }
                else
                {
                    notEmptyOrders.Add(order);
                }
            }
            //await DepositoryRecreateOrderTxs(notEmptyOrders); // already sent

            _context.Deals.RemoveRange(deals);
            _context.DealCopies.RemoveRange(await _context.DealCopies
                .Where(_ => dealIds.Contains(_.DealId)).ToListAsync());
            await _context.SaveChangesAsync();

            _logger.LogWarning($"DeleteDeals() DealIds:\n{string.Join(",", dealIds.Select(_ => $"'{_}'"))}");
            _logger.LogWarning($"DeleteDeals() emptyOrders:\n{string.Join(",", emptyOrders.Select(_ => $"'{_.Id}'"))}");
            _logger.LogWarning($"DeleteDeals() notEmptyOrders:\n{string.Join(",", notEmptyOrders.Select(_ => $"'{_.Id}'"))}");
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

        private async Task DepositoryRecreateOrderTxs(List<Order> orders)
        {
            List<TxCommandDto> orderTxCommands = new();
            foreach (var order in orders)
            {
                string userCurrencyCode = order.IsBid
                    ? CurrencyPair.GetCurrencyFromId(order.CurrencyPairCode)
                    : CurrencyPair.GetCurrencyToId(order.CurrencyPairCode);
                decimal blockAmount = order.IsBid
                    ? (order.Amount * order.Price).RoundDown(CurrenciesCache.Digits)
                    : order.Amount.RoundDown(CurrenciesCache.Digits);
                orderTxCommands.Add(new TxCommandDto
                {
                    TxTypeCode = TransactionType.OrderingBegin.Code,
                    CurrencyCode = userCurrencyCode,
                    Amount = blockAmount,
                    UserId = order.UserId,
                    ActionId = order.Id.ToString(),
                });
                orderTxCommands.Add(new TxCommandDto
                {
                    TxTypeCode = TransactionType.OrderingEnd.Code,
                    CurrencyCode = userCurrencyCode,
                    Amount = blockAmount,
                    UserId = order.UserId,
                    ActionId = order.Id.ToString(),
                });
            }

            foreach (var command in orderTxCommands)
                _logger.LogInformation($"OrderTx: {command}");

            var orderTxsResult = await _depositoryClient.SendTxCommands(orderTxCommands, false);
            _logger.LogWarning($"DeleteDeals() orderTxsResult: {orderTxsResult}");
        }
    }
}

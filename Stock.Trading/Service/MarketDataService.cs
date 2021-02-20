using MatchingEngine.Data;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class MarketDataService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly ILogger _logger;

        public MarketDataService(IServiceScopeFactory serviceScopeFactory,
            GatewayHttpClient gatewayHttpClient,
            ILogger<MarketDataService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _gatewayHttpClient = gatewayHttpClient;
            _logger = logger;
        }

        public async Task<bool> SaveOrdersFromEvents(List<OrderEvent> events)
        {
            try
            {
                _logger.LogDebug($"SaveOrdersFromEvents() sending events:{events.Count}");
                await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/order-events", events);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "");
                return false;
            }
        }

        public async Task SendOldOrders(List<Order> orders)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/orders/old-orders", orders);
        }

        public async Task SendActiveOrders(string currencyPairCode, List<Order> orders)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/orders?currencyPairCode={currencyPairCode}", orders);
        }

        public async Task SendNewDeal(DealResponse deal)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/deal", deal);
        }

        public async Task SendDealsFromDate(DateTimeOffset from, int pageSize = 1000)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                var query = context.Deals.Where(_ => _.DateCreated >= from);
                int pagesCount = (int)Math.Ceiling((await query.CountAsync()) * 1.0 / pageSize);
                for (int page = 0; page < pagesCount; page++)
                {
                    var deals = await query.Include(_ => _.Bid).Include(_ => _.Ask)
                        .OrderBy(_ => _.DateCreated)
                        .Skip(page * pageSize).Take(pageSize)
                        .ToListAsync();
                    _logger.LogInformation($"SendDeals to MarketData. page:{page}, count:{deals.Count}, " +
                        $"firstDate:{deals.First().DateCreated:o}");
                    await SendDeals(deals.Select(_ => _.GetDealResponse()));
                }
            }
        }

        public async Task SendDeals(IEnumerable<DealResponse> deals)
        {
            await _gatewayHttpClient.PostJsonAsync($"marketdata/deals", deals);
        }
    }
}

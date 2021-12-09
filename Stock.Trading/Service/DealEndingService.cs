using AutoMapper;
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

        Task SendOrderCancellings();
    }

    public class DealEndingService : IDealEndingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        private const int _batchSize = 100;
        private static bool _isSendingOrderCancellings = false;
        private static bool _isSendingDeals = false;

        public DealEndingService(
            IServiceScopeFactory serviceScopeFactory,
            GatewayHttpClient gatewayHttpClient,
            IMapper mapper,
            ILogger<DealEndingService> logger)
        {
            _scopeFactory = serviceScopeFactory;
            _gatewayHttpClient = gatewayHttpClient;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task SendDeal(Deal deal)
        {
            await _gatewayHttpClient.PostJsonAsync($"dealending/deal", deal);
        }

        public async Task SendDeals()
        {
            if (_isSendingDeals)
                return;
            try
            {
                _isSendingDeals = true;
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                    .Where(_ => !_.IsSentToDealEnding)
                    .OrderByDescending(_ => _.DateCreated)
                    .Take(_batchSize)
                    .ToListAsync();
                if (unprocessedDeals.Count == 0)
                    return;
                _logger.LogDebug($"SendDeals() unprocessed:{unprocessedDeals.Count}");

                int errorsCount = 0;
                foreach (var deal in unprocessedDeals)
                {
                    try
                    {
                        await SendDeal(deal);
                        deal.IsSentToDealEnding = true;
                        await context.SaveChangesAsync();
                        _logger.LogDebug($"SendDeals() sent: {deal.DealId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                        errorsCount++;
                        if (errorsCount > 5)
                            break;
                    }
                }
                if (errorsCount > 0)
                    _logger.LogInformation($"SendDeals() end. processed:{unprocessedDeals.Count}, with errors: {errorsCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
            _isSendingDeals = false;
        }

        public async Task<IFlurlResponse> CompleteOrderCancelling(MatchingOrder order)
        {
            return await $"dealending/orders/cancel".InternalApi()
                .PostJsonAsync(order);
        }

        public async Task SendOrderCancellings()
        {
            if (_isSendingOrderCancellings)
                return;
            try
            {
                _isSendingOrderCancellings = true;
                while (true)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

                        var unprocessedEvents = await context.OrderEvents
                            .Where(_ => !_.IsSentToDealEnding && _.EventType == OrderEventType.Cancel)
                            .OrderByDescending(_ => _.DateCreated)
                            .Take(_batchSize).ToListAsync();
                        if (unprocessedEvents.Count == 0)
                            break;
                        _logger.LogInformation($"SendOrderCancellings() unprocessed:{unprocessedEvents.Count}");

                        int errorsCount = 0;
                        foreach (var orderEvent in unprocessedEvents)
                        {
                            try
                            {
                                var result = await CompleteOrderCancelling(_mapper.Map<OrderEvent, MatchingOrder>(orderEvent));
                                orderEvent.IsSentToDealEnding = true;
                                await context.SaveChangesAsync();
                                _logger.LogDebug($"SendOrderCancellings() sent: {orderEvent}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"SendOrderCancellings Error: {orderEvent}");
                                errorsCount++;
                            }
                        }
                        if (errorsCount > 0)
                            _logger.LogWarning($"SendDeals() end. processed:{unprocessedEvents.Count}, with errors: {errorsCount}");
                    }
                    await Task.Delay(200);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
            _isSendingOrderCancellings = false;
        }
    }
}

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Services
{
    public interface IDealEndingService
    {
        Task SendDeal(Deal deal, TradingDbContext context = null);

        Task SendDeals();

        Task CompleteOrderCancelling(OrderEvent orderEvent, TradingDbContext context = null);

        Task SendOrderCancellings();
    }

    public class DealEndingService : IDealEndingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GatewayHttpClient _gatewayHttpClient;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        private const int _batchSize = 50;
        private static bool _isSendingOrderCancellings = false;
        private static bool _isSendingDeals = false;

        private static ConcurrentDictionary<Guid, bool> _sendingDealIds = new();
        private static ConcurrentDictionary<Guid, bool> _sendingCancellingIds = new();

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

        public async Task SendDeal(Deal deal, TradingDbContext context = null)
        {
            if (_sendingDealIds.ContainsKey(deal.DealId))
                return; // prevent double sending
            try
            {
                _sendingDealIds[deal.DealId] = true;

                await _gatewayHttpClient.PostJsonAsync($"dealending/deal", deal);
                deal.IsSentToDealEnding = true;
                if (context == null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                    context.Update(deal);
                    await context.SaveChangesAsync();
                }
                else
                {
                    await context.SaveChangesAsync();
                }
                _logger.LogDebug($"SendDeal() sent: {deal.DealId}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{deal}");
            }
            finally
            {
                _sendingDealIds.TryRemove(deal.DealId, out _);
            }
        }

        public async Task SendDeals()
        {
            if (_isSendingDeals)
                return;
            try
            {
                _isSendingDeals = true;
                int errorsCount = 0;
                while (errorsCount == 0)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                        var maxDate = DateTimeOffset.UtcNow.AddSeconds(-15); // don't send just created events because they send themselves
                        var unprocessedDeals = await context.Deals.Include(_ => _.Bid).Include(_ => _.Ask)
                            .Where(_ => !_.IsSentToDealEnding && _.DateCreated < maxDate)
                            .OrderByDescending(_ => _.DateCreated)
                            .Take(_batchSize)
                            .ToListAsync();
                        if (unprocessedDeals.Count == 0)
                            break;
                        _logger.LogDebug($"SendDeals() unprocessed:{unprocessedDeals.Count}");
                        errorsCount = 0;
                        foreach (var deal in unprocessedDeals)
                        {
                            try
                            {
                                await SendDeal(deal, context);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error sending to DealEnding: {deal}");
                                errorsCount++;
                            }
                        }
                        if (errorsCount > 0)
                            _logger.LogInformation($"SendDeals() end. processed:{unprocessedDeals.Count}, with errors: {errorsCount}");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
            _isSendingDeals = false;
        }

        public async Task CompleteOrderCancelling(OrderEvent orderEvent, TradingDbContext context = null)
        {
            if (_sendingCancellingIds.ContainsKey(orderEvent.Id))
                return; // prevent double sending
            try
            {
                _sendingCancellingIds[orderEvent.Id] = true;

                await $"dealending/orders/cancel".InternalApi()
                    .PostJsonAsync(_mapper.Map<OrderEvent, MatchingOrder>(orderEvent));
                orderEvent.IsSentToDealEnding = true;
                if (context == null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                    context.Update(orderEvent);
                    await context.SaveChangesAsync();
                }
                else
                {
                    await context.SaveChangesAsync();
                }
                _logger.LogDebug($"SendOrderCancelling() sent: {orderEvent}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{orderEvent}");
            }
            finally
            {
                _sendingCancellingIds.TryRemove(orderEvent.Id, out _);
            }
        }

        public async Task SendOrderCancellings()
        {
            if (_isSendingOrderCancellings)
                return;
            try
            {
                _isSendingOrderCancellings = true;
                int errorsCount = 0;
                bool isFullResendAttempt = new Random().Next(20) == 0;
                while (true)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                    var maxDate = DateTimeOffset.UtcNow.AddSeconds(-15); // don't send just created events because they send themselves
                    var unprocessedEvents = await context.OrderEvents
                        .Where(_ => !_.IsSentToDealEnding && _.EventType == OrderEventType.Cancel && _.DateCreated < maxDate)
                        .OrderByDescending(_ => _.DateCreated)
                        .Take(_batchSize).ToListAsync();
                    if (unprocessedEvents.Count == 0)
                        break;
                    _logger.LogDebug($"SendOrderCancellings() unprocessed:{unprocessedEvents.Count}");

                    errorsCount = 0;
                    foreach (var orderEvent in unprocessedEvents)
                    {
                        try
                        {
                            await CompleteOrderCancelling(orderEvent, context);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"SendOrderCancellings Error: {orderEvent}");
                            errorsCount++;
                        }
                    }
                    _logger.LogInformation($"SendDeals() end. processed:{unprocessedEvents.Count}, " +
                        $"with errors: {errorsCount}, isFullResendAttempt:{isFullResendAttempt}");
                    if (errorsCount == unprocessedEvents.Count) // DealEnding doesn't work at all
                        break;
                    if (errorsCount > 0 && !isFullResendAttempt)
                        break;
                    await Task.Delay(TimeSpan.FromSeconds(10));
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

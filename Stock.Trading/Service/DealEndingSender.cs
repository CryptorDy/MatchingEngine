using MatchingEngine.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Services
{
    public class DealEndingSender : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;

        public DealEndingSender(
            IServiceScopeFactory scopeFactory,
            ILogger<DealEndingSender> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var dealEndingService = scope.ServiceProvider.GetRequiredService<IDealEndingService>();
                await dealEndingService.SendDeals();
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken).ContinueWith(task => { });
            }
        }
    }
}

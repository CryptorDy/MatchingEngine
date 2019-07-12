using MatchingEngine.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stock.Trading.Integration.Tests
{
    [Collection(nameof(TestContext))]
    public class DealIntegrationTests
    {
        public DealIntegrationTests(TestContext testContext)
        {
            _testContext = testContext;
        }

        private readonly TestContext _testContext;

        [Fact]
        public async Task CanCreateReadAndDeleteDeal()
        {
            // Create bid
            var json = JsonConvert.SerializeObject(new OrderCreateRequest
            {
                ActionId = Guid.NewGuid().ToString(),
                IsBid = true,
                Amount = 1,
                Price = 1,
                CurrencyPairCode = "ETH_BTC",
                DateCreated = DateTimeOffset.UtcNow,
                UserId = "userId"
            });

            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _testContext.Client.PostAsync("/api/order", stringContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Create ask
            json = JsonConvert.SerializeObject(new OrderCreateRequest
            {
                ActionId = Guid.NewGuid().ToString(),
                IsBid = false,
                Amount = 1.5m,
                Price = 1,
                CurrencyPairCode = "ETH_BTC",
                DateCreated = DateTimeOffset.UtcNow,
                UserId = "userId"
            });

            stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            response = await _testContext.Client.PostAsync("/api/order", stringContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Wait deal
            await Task.Delay(TimeSpan.FromSeconds(40));

            // Get deals
            response = await _testContext.Client.GetAsync("/api/deal");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var deals = JsonConvert.DeserializeObject<List<DealResponse>>(content);
            Assert.NotEmpty(deals);
            Assert.Single(deals);
        }
    }
}

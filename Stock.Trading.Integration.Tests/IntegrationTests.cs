using MatchingEngine.Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stock.Trading.Integration.Tests
{
    [Collection(nameof(TestContext))]
    public class IntegrationTests
    {
        public IntegrationTests(TestContext testContext)
        {
            _testContext = testContext;
        }

        private readonly TestContext _testContext;

        [Fact]
        public async Task CanCreateReadAndCancelBid()
        {
            CanCreateReadAndCancelOrder(true);
        }

        [Fact]
        public async Task CanCreateReadAndCancelAsk()
        {
            CanCreateReadAndCancelOrder(false);
        }

        public async Task CanCreateReadAndCancelOrder(bool isBid)
        {
            var json = JsonConvert.SerializeObject(new OrderCreateRequest
            {
                ActionId = Guid.NewGuid().ToString(),
                IsBid = isBid,
                Amount = 1,
                Price = 1,
                CurrencyPairCode = "ETH_BTC",
                DateCreated = DateTimeOffset.UtcNow,
                UserId = "userId"
            });

            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _testContext.Client.PostAsync("/api/order", stringContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            string id = result.id;

            response = await _testContext.Client.GetAsync($"/api/order/{isBid}/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            var order = JsonConvert.DeserializeObject<Order>(content);
            Assert.NotNull(order);
            Assert.Equal(id, order.Id.ToString());

            response = await _testContext.Client.DeleteAsync($"/api/order/{isBid}/{id}?userId=userId");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await _testContext.Client.GetAsync($"/api/order/{isBid}/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            order = JsonConvert.DeserializeObject<Order>(content);
            Assert.NotNull(order);
            Assert.True(order.IsCanceled);
        }

        [Fact]
        public async Task GetCorrectAnswerFromHealthCheck()
        {
            var response = await _testContext.Client.GetAsync("/api/healthcheck");

            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"testhost\"", await response.Content.ReadAsStringAsync());
        }
    }
}

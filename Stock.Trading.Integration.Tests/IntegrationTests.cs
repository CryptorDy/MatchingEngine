using Newtonsoft.Json;
using Stock.Trading.Data.Entities;
using Stock.Trading.Requests;
using Stock.Trading.Responses;
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
        public async Task CanCreateReadAndCancelAsk()
        {
            var json = JsonConvert.SerializeObject(new AddRequest
            {
                Amount = 1,
                CurrencyPariId = "1",
                Price = 1,
                UserId = "userId"
            });

            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _testContext.Client.PostAsync("/api/ask", stringContent);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            string id = result.id;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await _testContext.Client.GetAsync($"/api/ask/{id}");

            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            var ask = JsonConvert.DeserializeObject<AskResponse>(content);
            Assert.NotNull(ask);
            Assert.Equal(id, ask.Id);

            response = await _testContext.Client.DeleteAsync($"/api/ask/{id}?userId=userId");

            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await _testContext.Client.GetAsync($"/api/ask/{id}");

            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            ask = JsonConvert.DeserializeObject<AskResponse>(content);
            Assert.NotNull(ask);
            Assert.Equal(ask.OrderTypeId, OrderType.Canceled.Code);
        }

        [Fact]
        public async Task CanCreateReadAndCancelBid()
        {
            var json = JsonConvert.SerializeObject(new AddRequest
            {
                Amount = 1,
                CurrencyPariId = "1",
                Price = 1,
                UserId = "userId"
            });

            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _testContext.Client.PostAsync("/api/bid", stringContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            string id = result.id;

            response = await _testContext.Client.GetAsync($"/api/bid/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            content = await response.Content.ReadAsStringAsync();
            var bid = JsonConvert.DeserializeObject<BidResponse>(content);
            Assert.NotNull(bid);
            Assert.Equal(id, bid.Id);

            response = await _testContext.Client.DeleteAsync($"/api/bid/{id}?userId=userId");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await _testContext.Client.GetAsync($"/api/bid/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            bid = JsonConvert.DeserializeObject<BidResponse>(content);
            Assert.NotNull(bid);
            Assert.Equal(bid.OrderTypeId, OrderType.Canceled.Code);
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

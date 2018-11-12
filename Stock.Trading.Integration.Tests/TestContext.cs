using System;
using System.Net.Http;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Stock.Trading.Integration.Tests
{
    public class TestContext : IDisposable
    {
        private readonly TestServer _server;

        public TestContext()
        {
            var webHostBuilder =
                WebHost.CreateDefaultBuilder()
                    .UseStartup<Startup>()
                    .UseEnvironment("Testing");

            _server = new TestServer(webHostBuilder);

            Client = _server.CreateClient();
        }

        public HttpClient Client { get; }

        public void Dispose()
        {
            _server?.Dispose();
            Client?.Dispose();
        }
    }
}

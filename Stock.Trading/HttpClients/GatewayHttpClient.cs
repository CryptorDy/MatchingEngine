using Microsoft.Extensions.Options;
using Stock.Trading.Models;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Stock.Trading.HttpClients
{
    public class GatewayHttpClient : HttpClientBase
    {
        public GatewayHttpClient(IOptions<AppSettings> settings) : base(settings.Value.GatewayServiceUrl)
        {
        }
    }
}
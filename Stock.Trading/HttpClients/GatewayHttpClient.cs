using Microsoft.Extensions.Options;

namespace Stock.Trading.HttpClients
{
    public class GatewayHttpClient : HttpClientBase
    {
        public GatewayHttpClient(IOptions<AppSettings> settings) : base(settings.Value.GatewayServiceUrl)
        {
        }
    }
}

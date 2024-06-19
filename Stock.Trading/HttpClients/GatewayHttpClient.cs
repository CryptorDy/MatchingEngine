using Microsoft.Extensions.Options;

namespace MatchingEngine.HttpClients
{
    public class GatewayHttpClient : HttpClientBase
    {
        public GatewayHttpClient(IOptions<AppSettings> settings) : base(settings.Value.GatewayServiceUrl)
        {
        }
    }
}

namespace MatchingEngine
{
    public class AppSettings
    {
        public string GatewayServiceUrl { get; set; }

        public int ImportedOrderbooksExpirationMinutes { get; set; }
        public int ImportedOrdersExpirationMinutes { get; set; }
    }
}

namespace MatchingEngine
{
    public class AppSettings
    {
        public string ConnectionString { get; internal set; }
        public string GatewayServiceUrl { get; set; }

        public int ImportedOrderbooksExpirationMinutes { get; set; }
        public int ImportedOrdersExpirationMinutes { get; set; }
    }
}

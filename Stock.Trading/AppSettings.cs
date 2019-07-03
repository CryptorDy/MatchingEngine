namespace Stock.Trading
{
    public class AppSettings
    {
        public string MarketDataServiceUrl { get; set; }
        public string BrokerageServiceUrl { get; set; }
        public string ConnectionString { get; internal set; }
        public string GatewayServiceUrl { get; set; }

        public int ImportedOrderbooksExpirationMinutes { get; set; }
        public int ImportedOrdersExpirationMinutes { get; set; }
    }
}

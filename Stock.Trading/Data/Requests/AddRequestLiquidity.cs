namespace Stock.Trading.Requests
{
    /// <summary>
    /// Model for saving orders from LiquidityImport
    /// </summary>
    public class AddRequestLiquidity : AddRequest
    {
        public bool IsBid { get; set; }

        public string TradingOrderId { get; set; }
    }
}

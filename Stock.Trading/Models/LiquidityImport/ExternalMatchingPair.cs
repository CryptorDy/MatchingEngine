namespace Stock.Trading.Models.LiquidityImport
{
    public class ExternalMatchingPair
    {
        public MOrder Bid { get; set; }
        public MOrder Ask { get; set; }
    }
}

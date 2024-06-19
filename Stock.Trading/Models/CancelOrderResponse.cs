namespace MatchingEngine.Models
{
    public class CancelOrderResponse
    {
        public CancelOrderResponseStatus Status { get; set; }
        public MatchingOrder Order { get; set; }
    }

    public enum CancelOrderResponseStatus { Success, AlreadyCanceled, AlreadyFilled, LiquidityBlocked, Error }
}

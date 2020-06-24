namespace MatchingEngine.Models
{
    public class CancelOrderResponse
    {
        public CancelOrderResponseStatus Status { get; set; }
        public Order Order { get; set; }
    }

    public enum CancelOrderResponseStatus { Success, AlreadyCanceled, AlreadyFilled, LiquidityBlocked, Error }
}

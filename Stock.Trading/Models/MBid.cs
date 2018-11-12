namespace Stock.Trading.Models
{
    public class MBid : MOrder
    {
        public MBid()
        {
            IsBid = true;
        }
    }
}
namespace MatchingEngine.Models.LiquidityImport
{
    public class SaveExternalOrderResult
    {
        public string NewExternalOrderId { get; set; }
        public string CreatedDealId { get; set; }

        public override string ToString() =>
            $"{nameof(SaveExternalOrderResult)}(NewExternalOrderId: {NewExternalOrderId}, DealId:{CreatedDealId})";
    }
}

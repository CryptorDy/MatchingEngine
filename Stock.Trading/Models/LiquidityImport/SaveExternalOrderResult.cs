using System;

namespace MatchingEngine.Models.LiquidityImport
{
    public class SaveExternalOrderResult
    {
        public Guid? NewExternalOrderId { get; set; }
        public Guid? CreatedDealId { get; set; }

        public override string ToString() =>
            $"{nameof(SaveExternalOrderResult)}(NewExternalOrderId: {NewExternalOrderId}, DealId:{CreatedDealId})";
    }
}

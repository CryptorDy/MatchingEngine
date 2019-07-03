using System;

namespace Stock.Trading.Models.LiquidityImport
{
    public class CurrencyPairExpiration
    {
        public bool IsExecuted { get; set; } = false;
        public Exchange Exchange { get; set; }

        public string CurrencyPairCode { get; set; }

        public DateTime ExpirationDate { get; set; }
    }
}

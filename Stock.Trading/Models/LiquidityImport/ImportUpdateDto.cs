using System.Collections.Generic;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ImportUpdateDto
    {
        public string CurrencyPairCode { get; set; }
        public List<OrderCreateRequest> OrdersToAdd { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToUpdate { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToDelete { get; set; } = new List<OrderCreateRequest>();
    }
}

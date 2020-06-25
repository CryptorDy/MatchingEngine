using System.Collections.Generic;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ImportUpdateDto
    {
        public List<OrderCreateRequest> OrdersToAdd { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToUpdate { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToDelete { get; set; } = new List<OrderCreateRequest>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models.LiquidityImport
{
    public class ImportUpdateDto
    {
        public List<OrderCreateRequest> OrdersToAdd { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToUpdate { get; set; } = new List<OrderCreateRequest>();
        public List<OrderCreateRequest> OrdersToDelete { get; set; } = new List<OrderCreateRequest>();
    }
}

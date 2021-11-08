using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public enum PoolBufferModelType
    {
        CreateOrder = 10, CancelOrder = 20, 
    }

    public class PoolBufferAction
    {
        public PoolBufferModelType ActionType { get; set; }
        public Guid OrderId { get; set; }

        // null if CancelOrder
        public MatchingOrder Order { get; set; }
    }
}

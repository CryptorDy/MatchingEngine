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

        /// <summary>null if CancelOrder</summary>
        public MatchingOrder Order { get; set; }

        /// <summary>Overwrites liquidity block on cancel if true</summary>
        public bool ToForce { get; set; }
    }
}

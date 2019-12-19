using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public class CancelOrderResponse
    {
        public CancelOrderResponseStatus Status { get; set; }
        public Order Order { get; set; }
    }

    public enum CancelOrderResponseStatus { Success, AlreadyCanceled, AlreadyFilled, LiquidityBlocked, Error }
}

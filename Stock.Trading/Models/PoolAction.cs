using MatchingEngine.Models.LiquidityImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Models
{
    public enum PoolActionType
    {
        CreateOrder = 10, CancelOrder = 20,
        UpdateLiquidityOrder = 100, RemoveLiquidityOrder = 110, // Liquidity import
        ExternalTradeResult = 150, AutoUnblockOrder = 160, // Liquidity trades
    }

    public class PoolAction
    {
        public PoolAction()
        {
        }

        public PoolAction(PoolActionType actionType, Guid orderId, MatchingOrder order = null)
        {
            ActionType = actionType;
            OrderId = orderId;
            Order = order;
        }

        public PoolActionType ActionType { get; set; }
        public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
        public Guid OrderId { get; set; }

        /// <summary>null if CancelOrder</summary>
        public MatchingOrder Order { get; set; }

        /// <summary>Overwrites liquidity block on cancel if true</summary>
        public bool ToForce { get; set; }

        public ExternalTrade ExternalTrade { get; set; }

        public override string ToString() =>
            $"{nameof(PoolAction)}({ActionType}, orderId:{OrderId}, order:{Order})";
    }
}

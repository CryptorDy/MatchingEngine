using MatchingEngine.Models.LiquidityImport;
using System;
using System.ComponentModel.DataAnnotations;

namespace MatchingEngine.Models
{
    public enum OrderEventType
    {
        Create, Cancel, Fulfill, Block, Unblock
    }

    public class OrderEvent
    {
        [Key]
        public Guid EventId { get; set; }

        public DateTimeOffset EventDate { get; set; }

        public OrderEventType EventType { get; set; }

        /// <summary>
        /// Related deals that were created with this event
        /// </summary>
        public string EventDealIds { get; set; }

        /// <summary>
        /// Is saved in MarketData DB
        /// </summary>
        public bool IsSavedInMarketData { get; set; }

        #region order copy fields

        public Guid Id { get; set; }

        public bool IsBid { get; set; }

        public decimal Price { get; set; }

        /// <summary>
        /// Base currency amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Executed amount
        /// </summary>
        public decimal Fulfilled { get; set; }

        /// <summary>
        /// Amount that is being processed by LiquidityImport
        /// </summary>
        public decimal Blocked { get; set; }

        [Required]
        public string CurrencyPairCode { get; set; }

        public DateTimeOffset DateCreated { get; set; }

        public ClientType ClientType { get; set; }

        [Required]
        public string UserId { get; set; }

        public bool IsCanceled { get; set; }

        /// <summary>
        /// Original order exchange
        /// </summary>
        public Exchange Exchange { get; set; } = Exchange.Local;

        /// <summary>
        /// How many times was this order sent to other exchange
        /// </summary>
        public int LiquidityBlocksCount { get; set; }

        public decimal AvailableAmount => (Amount - Fulfilled - Blocked);

        public bool IsActive => !IsCanceled && Fulfilled < Amount;

        public bool IsLocal => Exchange == Exchange.Local;

        #endregion order copy fields

        public override string ToString() => $"Event {EventType} {EventId} for {(IsBid ? "Bid" : "Ask")}({Id} {CurrencyPairCode})";

        public static OrderEvent Create(AutoMapper.IMapper mapper, Order order, OrderEventType type, string dealIds = null)
        {
            var orderEvent = mapper.Map<Order, OrderEvent>(order);
            orderEvent.EventType = type;
            orderEvent.IsSavedInMarketData = false;
            orderEvent.EventDealIds = string.IsNullOrWhiteSpace(dealIds) ? null : dealIds;
            return orderEvent;
        }
    }
}

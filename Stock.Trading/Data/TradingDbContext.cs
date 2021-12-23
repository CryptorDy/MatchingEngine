using AutoMapper;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Data
{
    public class TradingDbContext : DbContext
    {
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        public TradingDbContext(DbContextOptions opts,
            IMapper mapper,
            ILogger<TradingDbContext> logger) : base(opts)
        {
            _mapper = mapper;
            _logger = logger;
        }

        public virtual DbSet<Bid> Bids { get; set; }
        public virtual DbSet<Ask> Asks { get; set; }
        public virtual DbSet<OrderEvent> OrderEvents { get; set; }

        public virtual DbSet<Deal> Deals { get; set; }
        public virtual DbSet<DealCopy> DealCopies { get; set; }
        public virtual DbSet<MatchingExternalTrade> ExternalTrades { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Bid>().Property(e => e.IsActive2).UsePropertyAccessMode(PropertyAccessMode.Property);
            builder.Entity<Ask>().Property(e => e.IsActive2).UsePropertyAccessMode(PropertyAccessMode.Property);

            builder.Entity<OrderEvent>().Property(_ => _.EventDate)
                .ValueGeneratedOnAdd().HasDefaultValueSql("current_timestamp"); // set curent date
            builder.Entity<OrderEvent>().Property(_ => _.EventType)
                .HasConversion(new EnumToStringConverter<OrderEventType>()); // save enum as string
            builder.Entity<OrderEvent>().HasIndex(_ => _.EventType);
            builder.Entity<OrderEvent>().HasIndex(_ => _.IsSentToDealEnding);

            builder.Entity<Deal>().HasIndex(_ => _.IsSentToDealEnding);
            builder.Entity<Deal>().HasIndex(_ => _.FromInnerTradingBot);

            builder.Entity<MatchingExternalTrade>().HasKey(_ => _.Id);
            builder.Entity<MatchingExternalTrade>().Property(_ => _.DateCreated).ValueGeneratedOnAdd().HasDefaultValueSql("current_timestamp");
        }

        #region Order setters

        public async Task<MatchingOrder> AddOrder(MatchingOrder order, bool toSave, OrderEventType eventType)
        {
            MatchingOrder trackedOrder;
            if (order.IsBid)
            {
                trackedOrder = _mapper.Map<MatchingOrder, Bid>(order);
                Bids.Add((Bid)trackedOrder);
            }
            else
            {
                trackedOrder = _mapper.Map<MatchingOrder, Ask>(order);
                Asks.Add((Ask)trackedOrder);
            }

            _logger.LogDebug($"AddOrder() toSave:{toSave}, eventType:{eventType}, order:{order}");
            OrderEvents.Add(OrderEvent.Create(_mapper, trackedOrder, eventType));
            if (toSave)
            {
                await SaveChangesAsync();
            }
            return trackedOrder;
        }

        public async Task<OrderEvent> UpdateOrder(MatchingOrder order, bool toSave, OrderEventType eventType,
            bool isSentToDealEnding = false, string dealIds = null)
        {
            _logger.LogDebug($"UpdateOrder() toSave:{toSave}, eventType:{eventType}, order:{order}");
            Update(order);
            var orderEvent = OrderEvent.Create(_mapper, order, eventType, isSentToDealEnding, dealIds);
            OrderEvents.Add(orderEvent);
            if (toSave)
            {
                await SaveChangesAsync();
            }
            return orderEvent;
        }

        #endregion Order setters

        #region Order getters

        protected IQueryable<MatchingOrder> GetOrdersQuery(IQueryable<MatchingOrder> source, string currencyPairCode, int? count,
            string userId, OrderStatusRequest status,
            DateTimeOffset? from, DateTimeOffset? to)
        {
            var query = source.Include(_ => _.DealList).Where(_ =>
                (string.IsNullOrWhiteSpace(currencyPairCode) || _.CurrencyPairCode == currencyPairCode)
                && (string.IsNullOrWhiteSpace(userId) || _.UserId == userId)
                && (status == OrderStatusRequest.All
                    || (status == OrderStatusRequest.Active && !_.IsCanceled && _.Fulfilled < _.Amount)
                    || (status == OrderStatusRequest.Canceled && _.IsCanceled))
                && (!from.HasValue || _.DateCreated >= from) && (!to.HasValue || _.DateCreated <= to));
            query = query.OrderByDescending(_ => _.DateCreated).Take(count ?? int.MaxValue);
            return query;
        }

        public async Task<List<MatchingOrder>> GetOrders(bool? isBid = null, string currencyPairCode = null, int? count = Constants.DefaultRequestOrdersCount,
            string userId = null, OrderStatusRequest status = OrderStatusRequest.Active,
            DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            List<MatchingOrder> dbOrders = new List<MatchingOrder>();
            if (!isBid.HasValue || isBid.Value)
            {
                var bids = await GetOrdersQuery(Bids, currencyPairCode, count, userId, status, from, to).ToListAsync();
                dbOrders.AddRange(bids);
            }
            if (!isBid.HasValue || !isBid.Value)
            {
                var asks = await GetOrdersQuery(Asks, currencyPairCode, count, userId, status, from, to).ToListAsync();
                dbOrders.AddRange(asks);
            }
            return dbOrders;
        }

        public async Task<List<MatchingOrder>> LoadDbOrders(List<MatchingOrder> orders)
        {
            var bidIds = orders.Where(_ => _.IsBid).Select(_ => _.Id).ToList();
            var askIds = orders.Where(_ => !_.IsBid).Select(_ => _.Id).ToList();
            var dbBids = await Bids.Where(_ => bidIds.Contains(_.Id)).ToListAsync();
            var dbAsks = await Asks.Where(_ => askIds.Contains(_.Id)).ToListAsync();
            return dbBids.Cast<MatchingOrder>().Union(dbAsks).ToList();
        }

        public async Task<MatchingOrder> GetOrder(Guid orderId, bool? isBid = null)
        {
            MatchingOrder order = null;
            if (isBid == null || isBid == true)
                order = await Bids.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == orderId);
            if (isBid == false || (isBid == null && order == null))
                order = await Asks.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == orderId);
            return order;
        }

        #endregion Order getters
    }
}

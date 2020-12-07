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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Bid>().Property(e => e.IsActive2).UsePropertyAccessMode(PropertyAccessMode.Property);
            builder.Entity<Ask>().Property(e => e.IsActive2).UsePropertyAccessMode(PropertyAccessMode.Property);

            builder.Entity<OrderEvent>().Property(_ => _.EventDate)
                .ValueGeneratedOnAdd().HasDefaultValueSql("current_timestamp"); // set curent date
            builder.Entity<OrderEvent>().Property(_ => _.EventType)
                .HasConversion(new EnumToStringConverter<OrderEventType>()); // save enum as string
        }

        public async Task LogDealExists(Guid dealId, string place)
        {
            bool exists = await Deals.AnyAsync(_ => _.DealId == dealId);
            _logger.Log(exists ? LogLevel.Information : LogLevel.Error,
                $"LogDealExists() exists:{exists} '{place}' dealId:{dealId}");
        }

        #region Order setters

        public async Task<Order> AddOrder(Order order, bool toSave, OrderEventType eventType)
        {
            Order trackedOrder;
            if (order.IsBid)
            {
                trackedOrder = _mapper.Map<Order, Bid>(order);
                Bids.Add((Bid)trackedOrder);
            }
            else
            {
                trackedOrder = _mapper.Map<Order, Ask>(order);
                Asks.Add((Ask)trackedOrder);
            }

            OrderEvents.Add(OrderEvent.Create(_mapper, trackedOrder, eventType));
            _logger.LogInformation($"AddOrder() toSave:{toSave}, eventType:{eventType}, order:{order}");
            if (toSave)
            {
                await SaveChangesAsync();
            }
            return trackedOrder;
        }

        public async Task UpdateOrder(Order order, bool toSave, OrderEventType eventType, string dealIds = null)
        {
            if (order.IsBid)
            {
                Bids.Update((Bid)order);
            }
            else
            {
                Asks.Update((Ask)order);
            }

            OrderEvents.Add(OrderEvent.Create(_mapper, order, eventType, dealIds));

            if (toSave)
            {
                await SaveChangesAsync();
            }
        }

        #endregion Order setters

        #region Order getters

        protected IQueryable<Order> GetOrdersQuery(IQueryable<Order> source, string currencyPairCode, int? count,
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

        public async Task<List<Order>> GetOrders(bool? isBid = null, string currencyPairCode = null, int? count = Constants.DefaultRequestOrdersCount,
            string userId = null, OrderStatusRequest status = OrderStatusRequest.Active,
            DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            List<Order> dbOrders = new List<Order>();
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

        public async Task<List<Order>> LoadDbOrders(List<Order> orders)
        {
            var bidIds = orders.Where(_ => _.IsBid).Select(_ => _.Id).ToList();
            var askIds = orders.Where(_ => !_.IsBid).Select(_ => _.Id).ToList();
            var dbOrders = await Bids.Where(_ => bidIds.Contains(_.Id))
                .Cast<Order>()
                .Union(Asks.Where(_ => askIds.Contains(_.Id)))
                .ToListAsync();
            return dbOrders;
        }

        public async Task<Order> GetOrder(Guid orderId, bool? isBid = null)
        {
            Order order = null;
            if (isBid == null || isBid == true)
            {
                order = await Bids.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == orderId);
            }
            if (isBid == false || (isBid == null && order == null))
            {
                order = await Asks.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == orderId);
            }

            foreach (var deal in order.DealList)
            {
                deal.Ask = null;
                deal.Bid = null;
            }
            return order;
        }

        #endregion Order getters
    }
}

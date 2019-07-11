using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Data
{
    public class TradingDbContext : DbContext
    {
        private readonly IMapper _mapper;

        public TradingDbContext(DbContextOptions opts, IMapper mapper) : base(opts)
        {
            _mapper = mapper;
        }

        public DbSet<Bid> Bids { get; set; }
        public DbSet<Ask> Asks { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }

        public virtual DbSet<MatchingEngine.Models.Bid> BidsV2 { get; set; }
        public virtual DbSet<MatchingEngine.Models.Ask> AsksV2 { get; set; }
        public virtual DbSet<MatchingEngine.Models.Deal> DealsV2 { get; set; }

        public async Task<MatchingEngine.Models.Order> AddOrder(MatchingEngine.Models.Order order, bool toSave)
        {
            MatchingEngine.Models.Order trackedOrder;
            if (order.IsBid)
            {
                trackedOrder = _mapper.Map<MatchingEngine.Models.Order, MatchingEngine.Models.Bid>(order);
                BidsV2.Add((MatchingEngine.Models.Bid)trackedOrder);
            }
            else
            {
                trackedOrder = _mapper.Map<MatchingEngine.Models.Order, MatchingEngine.Models.Ask>(order);
                AsksV2.Add((MatchingEngine.Models.Ask)trackedOrder);
            }
            if (toSave)
                await SaveChangesAsync();
            return trackedOrder;
        }

        public async Task<List<MatchingEngine.Models.Order>> GetOrders(string userId = null)
        {
            var dbOrders = await BidsV2.Include(o => o.DealList).Where(_ => string.IsNullOrEmpty(userId) || _.UserId == userId)
                .Cast<MatchingEngine.Models.Order>()
                .Union(AsksV2.Include(o => o.DealList).Where(_ => string.IsNullOrEmpty(userId) || _.UserId == userId))
                .ToListAsync();

            foreach (var order in dbOrders)  // remove circular dependency to prevent json error
            {
                foreach (var deal in order.DealList)
                {
                    deal.Ask = null;
                    deal.Bid = null;
                }
            }
            return dbOrders;
        }

        public async Task<List<MatchingEngine.Models.Order>> GetOrders(List<MatchingEngine.Models.Order> orders)
        {
            var bidIds = orders.Where(_ => _.IsBid).Select(_ => _.Id).ToList();
            var askIds = orders.Where(_ => !_.IsBid).Select(_ => _.Id).ToList();
            var dbOrders = await BidsV2.Where(_ => bidIds.Contains(_.Id))
                .Cast<MatchingEngine.Models.Order>()
                .Union(AsksV2.Where(_ => askIds.Contains(_.Id)))
                .ToListAsync();
            return dbOrders;
        }

        public async Task<MatchingEngine.Models.Order> GetOrder(bool isBid, Guid id)
        {
            MatchingEngine.Models.Order order;
            if (isBid)
                order = await BidsV2.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == id);
            else
                order = await AsksV2.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == id);

            foreach (var deal in order.DealList)
            {
                deal.Ask = null;
                deal.Bid = null;
            }
            return order;
        }
    }
}

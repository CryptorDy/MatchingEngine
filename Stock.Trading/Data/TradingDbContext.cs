using AutoMapper;
using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Stock.Trading.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Data
{
    public class TradingDbContext : DbContext
    {
        private readonly IMapper _mapper;

        public TradingDbContext(DbContextOptions opts, IMapper mapper) : base(opts)
        {
            _mapper = mapper;
        }

        public virtual DbSet<Bid> Bids { get; set; }
        public virtual DbSet<Ask> Asks { get; set; }
        public virtual DbSet<Deal> Deals { get; set; }

        public async Task<Order> AddOrder(Order order, bool toSave)
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
            if (toSave)
                await SaveChangesAsync();
            return trackedOrder;
        }

        public async Task UpdateOrder(Order order, bool toSave)
        {
            if (order.IsBid) Bids.Update((Bid)order);
            else Asks.Update((Ask)order);

            if (toSave)
                await SaveChangesAsync();
        }

        public async Task<List<Order>> GetOrders(bool isBid, string userId = null)
        {
            List<Order> dbOrders;
            if (isBid)
                dbOrders = await Bids.Include(o => o.DealList)
                    .Where(_ => _.IsBid == isBid && (string.IsNullOrEmpty(userId) || _.UserId == userId))
                    .Cast<Order>().ToListAsync();
            else
                dbOrders = await Asks.Include(o => o.DealList)
                    .Where(_ => _.IsBid == isBid && (string.IsNullOrEmpty(userId) || _.UserId == userId))
                    .Cast<Order>().ToListAsync();

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

        public async Task<List<Order>> GetOrders(List<Order> orders)
        {
            var bidIds = orders.Where(_ => _.IsBid).Select(_ => _.Id).ToList();
            var askIds = orders.Where(_ => !_.IsBid).Select(_ => _.Id).ToList();
            var dbOrders = await Bids.Where(_ => bidIds.Contains(_.Id))
                .Cast<Order>()
                .Union(Asks.Where(_ => askIds.Contains(_.Id)))
                .ToListAsync();
            return dbOrders;
        }

        public async Task<Order> GetOrder(bool isBid, Guid id)
        {
            Order order;
            if (isBid)
                order = await Bids.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == id);
            else
                order = await Asks.Include(o => o.DealList).FirstOrDefaultAsync(_ => _.Id == id);

            foreach (var deal in order.DealList)
            {
                deal.Ask = null;
                deal.Bid = null;
            }
            return order;
        }
    }
}

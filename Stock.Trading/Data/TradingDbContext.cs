using Microsoft.EntityFrameworkCore;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stock.Trading.Data
{
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions opts) : base(opts)
        {
        }

        public DbSet<Bid> Bids { get; set; }
        public DbSet<Ask> Asks { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }

        public virtual DbSet<MatchingEngine.Models.Order> BidsV2 { get; set; }
        public virtual DbSet<MatchingEngine.Models.Order> AsksV2 { get; set; }
        public virtual DbSet<MatchingEngine.Models.Deal> DealsV2 { get; set; }

        public async Task AddOrder(MatchingEngine.Models.Order order)
        {
            if (order.IsBid) BidsV2.Add(order);
            else AsksV2.Add(order);
            await SaveChangesAsync();
        }

        public async Task<List<MatchingEngine.Models.Order>> GetDbOrders(List<MatchingEngine.Models.Order> orders)
        {
            var bidIds = orders.Where(_ => _.IsBid).Select(_ => _.Id).ToList();
            var askIds = orders.Where(_ => !_.IsBid).Select(_ => _.Id).ToList();
            var dbOrders = await BidsV2.Where(_ => bidIds.Contains(_.Id))
                .Union(AsksV2.Where(_ => askIds.Contains(_.Id)))
                .ToListAsync();
            return dbOrders;
        }
    }
}

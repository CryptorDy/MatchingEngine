using Microsoft.EntityFrameworkCore;
using Stock.Trading.Data.Entities;
using Stock.Trading.Entities;
using Stock.Trading.Models;

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
    }
}
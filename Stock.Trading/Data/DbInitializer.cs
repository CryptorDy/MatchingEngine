using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MatchingEngine.Data
{
    public class DbInitializer : IDbInitializer
    {
        private readonly TradingDbContext _dbContext;

        public DbInitializer(TradingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Init()
        {
            _dbContext.Database.Migrate();

            await LiquidityUnblockAllDbOrders();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task LiquidityUnblockAllDbOrders()
        {
            var blockedBids = await _dbContext.Bids.Where(_ => _.Blocked > 0).ToListAsync();
            var blockedAsks = await _dbContext.Asks.Where(_ => _.Blocked > 0).ToListAsync();
            foreach (Order order in blockedBids.Cast<Order>().Union(blockedAsks))
                order.Blocked = 0;
            await _dbContext.SaveChangesAsync();
        }
    }

    public interface IDbInitializer
    {
        Task Init();
    }
}

using MatchingEngine.Models;
using MatchingEngine.Models.LiquidityImport;
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

        public void Seed()
        {
            _dbContext.Database.Migrate();
            SetClientTypes().Wait();
            SetDealIsProcessed().Wait();
        }

        public async Task SetClientTypes()
        {
            var bids = await _dbContext.Bids.ToListAsync();
            if (bids.Any(_ => _.ClientType != ClientType.User))
            {
                return;
            }
            var asks = await _dbContext.Asks.ToListAsync();
            foreach (var order in asks)
            {
                if (order.FromInnerTradingBot)
                {
                    order.ClientType = ClientType.DealsBot;
                }
                else if (order.Exchange != Exchange.Local)
                {
                    order.ClientType = ClientType.LiquidityBot;
                }
                else
                {
                    order.ClientType = ClientType.User;
                }
            }
            foreach (var order in bids)
            {
                if (order.FromInnerTradingBot)
                {
                    order.ClientType = ClientType.DealsBot;
                }
                else if (order.Exchange != Exchange.Local)
                {
                    order.ClientType = ClientType.LiquidityBot;
                }
                else
                {
                    order.ClientType = ClientType.User;
                }
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task SetDealIsProcessed()
        {
            var deals = await _dbContext.Deals.Where(_ => _.FromInnerTradingBot && !_.IsProcessed).ToListAsync();
            deals.ForEach(_ => _.IsProcessed = true);
            await _dbContext.SaveChangesAsync();
        }

        public void Init()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Database.Migrate();
        }
    }

    public interface IDbInitializer
    {
        void Seed();

        void Init();
    }
}

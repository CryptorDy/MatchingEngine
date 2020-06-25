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

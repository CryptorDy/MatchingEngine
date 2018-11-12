using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Stock.Trading.Data.Entities;

namespace Stock.Trading.Data
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

            if (_dbContext.OrderTypes.Any())
                return;

            var orderTypes = new List<OrderType>
            {
                OrderType.Active,
                OrderType.Blocked,
                OrderType.Canceled,
                OrderType.Completed
            };

            _dbContext.OrderTypes.AddRange(orderTypes);
            _dbContext.SaveChanges();
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

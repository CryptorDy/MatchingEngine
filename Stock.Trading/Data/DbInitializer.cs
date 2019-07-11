using MatchingEngine.Models;
using Microsoft.EntityFrameworkCore;
using Stock.Trading.Models.LiquidityImport;
using System.Collections.Generic;
using System.Linq;

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

            TransferOrdersAndDeals();
        }

        public void TransferOrdersAndDeals()
        {
            if (_dbContext.BidsV2.Any())
                return;
            var bids = _dbContext.Bids.ToList();
            foreach (var order in bids)
            {
                _dbContext.BidsV2.Add(new Bid
                {
                    Id = order.Id,
                    IsBid = true,
                    Price = order.Price,
                    Amount = order.Volume,
                    Fulfilled = order.OrderTypeCode == Data.Entities.OrderType.Completed.Code ? order.Volume : order.Fulfilled,
                    Blocked = 0,
                    CurrencyPairCode = order.CurrencyPairId,
                    DateCreated = order.OrderDateUtc,
                    UserId = order.UserId,
                    IsCanceled = order.OrderTypeCode == Data.Entities.OrderType.Canceled.Code,
                    Exchange = (Exchange)order.ExchangeId,
                    FromInnerTradingBot = order.FromInnerTradingBot,
                });
            }
            var asks = _dbContext.Asks.ToList();
            foreach (var order in asks)
            {
                _dbContext.AsksV2.Add(new Ask
                {
                    Id = order.Id,
                    IsBid = true,
                    Price = order.Price,
                    Amount = order.Volume,
                    Fulfilled = order.OrderTypeCode == Data.Entities.OrderType.Completed.Code ? order.Volume : order.Fulfilled,
                    Blocked = 0,
                    CurrencyPairCode = order.CurrencyPairId,
                    DateCreated = order.OrderDateUtc,
                    UserId = order.UserId,
                    IsCanceled = order.OrderTypeCode == Data.Entities.OrderType.Canceled.Code,
                    Exchange = (Exchange)order.ExchangeId,
                    FromInnerTradingBot = order.FromInnerTradingBot,
                });
            }

            var deals = _dbContext.Deals.ToList();
            foreach (var deal in deals)
            {
                _dbContext.DealsV2.Add(new Deal
                {
                    DealId = deal.DealId,
                    Price = deal.Price,
                    Volume = deal.Volume,
                    DateCreated = deal.DealDateUtc,
                    BidId = deal.BidId,
                    AskId = deal.AskId,
                    FromInnerTradingBot = deal.FromInnerTradingBot,
                });
            }
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

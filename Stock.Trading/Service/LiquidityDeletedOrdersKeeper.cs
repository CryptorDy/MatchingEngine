using System;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public interface ILiquidityDeletedOrdersKeeper
    {
        void AddRange(IEnumerable<Guid> ids);

        bool Contains(Guid id);
    }

    public class LiquidityDeletedOrdersKeeper : ILiquidityDeletedOrdersKeeper
    {
        private readonly Dictionary<Guid, DateTime> _liquidityDeletedOrderIds = new Dictionary<Guid, DateTime>();

        public LiquidityDeletedOrdersKeeper()
        {
        }

        public bool Contains(Guid id)
        {
            lock (_liquidityDeletedOrderIds)
            {
                return _liquidityDeletedOrderIds.ContainsKey(id);
            }
        }

        public void AddRange(IEnumerable<Guid> ids)
        {
            RemoveOldIds();

            lock (_liquidityDeletedOrderIds)
            {
                foreach (Guid orderId in ids)
                {
                    _liquidityDeletedOrderIds.TryAdd(orderId, DateTime.Now);
                }
            }
        }

        private void RemoveOldIds()
        {
            lock (_liquidityDeletedOrderIds)
            {
                var oldIds = _liquidityDeletedOrderIds.Where(_ => _.Value < DateTime.Now.AddMinutes(-10)).ToList();
                oldIds.ForEach(_ => _liquidityDeletedOrderIds.Remove(_.Key));
            }
        }
    }
}

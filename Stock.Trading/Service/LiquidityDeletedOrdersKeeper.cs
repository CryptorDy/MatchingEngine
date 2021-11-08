using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatchingEngine.Services
{
    public interface ILiquidityDeletedOrdersKeeper
    {
        void Add(Guid orderId);

        void AddRange(IEnumerable<Guid> ids);

        bool Contains(Guid id);
    }

    public class LiquidityDeletedOrdersKeeper : ILiquidityDeletedOrdersKeeper
    {
        private readonly ConcurrentDictionary<Guid, DateTime> _liquidityDeletedOrderIds =
            new ConcurrentDictionary<Guid, DateTime>();

        public LiquidityDeletedOrdersKeeper()
        {
        }

        public bool Contains(Guid id)
        {
            return _liquidityDeletedOrderIds.ContainsKey(id);
        }

        public void Add(Guid orderId)
        {
            RemoveOldIds();
            _liquidityDeletedOrderIds[orderId] = DateTime.Now;
        }

        public void AddRange(IEnumerable<Guid> ids)
        {
            RemoveOldIds();

            foreach (Guid orderId in ids)
                _liquidityDeletedOrderIds[orderId] = DateTime.Now;
        }

        private void RemoveOldIds()
        {
            var oldIds = _liquidityDeletedOrderIds.Where(_ => _.Value < DateTime.Now.AddMinutes(-10)).ToList();
            oldIds.ForEach(id => _liquidityDeletedOrderIds.TryRemove(id.Key, out _));
        }
    }
}

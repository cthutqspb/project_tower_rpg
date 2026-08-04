using System.Collections.Generic;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public static class UIRegistry
    {
        private static Dictionary<Entity, List<object>> _receivers = new();

        public static void Register(Entity entity, object receiver)
        {
            if (entity == Entity.Null || receiver == null) return;
            
            if (!_receivers.ContainsKey(entity))
                _receivers[entity] = new List<object>();
            
            if (!_receivers[entity].Contains(receiver))
                _receivers[entity].Add(receiver);
        }

        public static void Unregister(Entity entity, object receiver)
        {
            if (entity == Entity.Null || receiver == null) return;
            
            if (_receivers.TryGetValue(entity, out var list))
            {
                list.Remove(receiver);
                if (list.Count == 0)
                {
                    _receivers.Remove(entity);
                }
            }
        }

        public static List<object> GetReceivers(Entity entity)
        {
            return _receivers.TryGetValue(entity, out var list) ? list : null;
        }

        public static IEnumerable<Entity> GetActiveEntities()
        {
            return _receivers.Keys;
        }

        public static void Clear()
        {
            _receivers.Clear();
        }
    }
}

using System.Collections.Generic;
using Unity.Entities;

namespace ProjectTowerRpg.Core.Presentation
{
    public static class ProjectileViewRegistry
    {
        private static readonly Dictionary<Entity, SyncTransformWithEntity> _registry = new();

        public static void Register(Entity entity, SyncTransformWithEntity view)
        {
            if (entity == Entity.Null || view == null) return;
            _registry[entity] = view;
        }

        public static void Unregister(Entity entity)
        {
            if (entity == Entity.Null) return;
            _registry.Remove(entity);
        }

        public static bool TryGet(Entity entity, out SyncTransformWithEntity view)
        {
            if (entity == Entity.Null)
            {
                view = null;
                return false;
            }
            return _registry.TryGetValue(entity, out view);
        }

        public static SyncTransformWithEntity Get(Entity entity)
            => TryGet(entity, out var v) ? v : null;

        public static IEnumerable<Entity> GetEntities() => _registry.Keys;
        public static int Count => _registry.Count;
        public static void Clear() => _registry.Clear();
    }
}

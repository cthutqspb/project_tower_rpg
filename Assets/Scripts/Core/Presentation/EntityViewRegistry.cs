using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ProjectTowerRpg.Core.Presentation
{
    // 🦾 ТОТАЛЬНЫЙ СИ-МОСТ: универсальный реестр сущностей мира.
    public static class EntityViewRegistry
    {
        private static readonly Dictionary<Entity, Component> _registry = new();

        public static void Register(Entity entity, Component view)
        {
            if (entity == Entity.Null || view == null) return;
            _registry[entity] = view;
        }

        public static void Unregister(Entity entity)
        {
            if (entity == Entity.Null) return;
            _registry.Remove(entity);
        }

        public static bool TryGet(Entity entity, out Component view)
        {
            if (entity == Entity.Null)
            {
                view = null;
                return false;
            }
            return _registry.TryGetValue(entity, out view);
        }

        public static bool TryGet<T>(Entity entity, out T view) where T : Component
        {
            if (TryGet(entity, out var comp) && comp is T typed)
            {
                view = typed;
                return true;
            }
            view = null;
            return false;
        }

        public static Component Get(Entity entity)
            => TryGet(entity, out var v) ? v : null;

        public static T Get<T>(Entity entity) where T : Component
            => TryGet<T>(entity, out var v) ? v : null;

        public static IEnumerable<Entity> GetEntities()
        {
            return _registry.Keys;
        }

        public static int Count => _registry.Count;

        public static void Clear()
        {
            _registry.Clear();
        }
    }
}

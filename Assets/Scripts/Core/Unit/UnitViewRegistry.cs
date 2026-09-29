using System.Collections.Generic;
using Unity.Entities;
using ProjectTowerRpg.ECS.Systems; // 🦾 СИ-ФИКС: Подключили пространство имён, где сидит UnitView

namespace ProjectTowerRpg.Core.Units
{
    public static class UnitViewRegistry
    {
        private static readonly Dictionary<Entity, UnitView> _registry = new Dictionary<Entity, UnitView>();

        public static void Register(Entity entity, UnitView view)
        {
            if (entity != Entity.Null && view != null)
            {
                _registry[entity] = view;
            }
        }

        public static void Unregister(Entity entity)
        {
            if (entity != Entity.Null)
            {
                _registry.Remove(entity);
            }
        }

        public static UnitView Get(Entity entity)
        {
            if (entity != Entity.Null && _registry.TryGetValue(entity, out var view))
            {
                return view;
            }
            return null;
        }

        public static void Clear()
        {
            _registry.Clear();
        }
    }
}


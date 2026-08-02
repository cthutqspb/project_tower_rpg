using Unity.Entities;
using System.Collections.Generic;
using UnityEngine; // Подключаем для GameObject
using ProjectTowerRpg.Core.UI.Components; // Подключаем для StaticGrid

namespace ProjectTowerRpg.Core.UI
{
    public static class EntityRegistry
    {
        // Храним связь: Строковый ID (для UI/Drag) -> ECS Entity
        private static Dictionary<string, Entity> _registry = new Dictionary<string, Entity>();
        
        // Храним обратную связь: ECS Entity сетки -> Экземпляр UI-компонента StaticGrid
        private static Dictionary<Entity, StaticGrid> _gridUiRegistry = new Dictionary<Entity, StaticGrid>();

        // Храним связь: ECS Entity предмета -> Его графический GameObject
        private static Dictionary<Entity, GameObject> _itemVisuals = new Dictionary<Entity, GameObject>();

        // ================================================================
        // РЕГИСТРАЦИЯ СУЩНОСТЕЙ
        // ================================================================
        public static void Register(string id, Entity entity)
        {
            if (string.IsNullOrEmpty(id)) return;
            _registry[id] = entity;
        }

        public static Entity Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return Entity.Null;
            return _registry.TryGetValue(id, out var entity) ? entity : Entity.Null;
        }

        // ================================================================
        // РЕГИСТРАЦИЯ UI СЕТОК (Для SyncAllGridsUiSystem)
        // ================================================================
        public static void RegisterGrid(Entity entity, StaticGrid grid)
        {
            if (entity == Entity.Null || grid == null) return;
            _gridUiRegistry[entity] = grid;
        }

        public static StaticGrid GetGrid(Entity entity)
        {
            if (entity == Entity.Null) return null;
            return _gridUiRegistry.TryGetValue(entity, out var grid) ? grid : null;
        }

        public static void UnregisterGrid(Entity entity)
        {
            _gridUiRegistry.Remove(entity);
        }

        // ================================================================
        // РЕГИСТРАЦИЯ ВИЗУАЛА ПРЕДМЕТОВ (Для Фабрики Отображения)
        // ================================================================
        public static void RegisterItemVisual(Entity entity, GameObject visual)
        {
            if (entity == Entity.Null || visual == null) return;
            _itemVisuals[entity] = visual;
        }

        public static GameObject GetItemVisual(Entity entity)
        {
            if (entity == Entity.Null) return null;
            return _itemVisuals.TryGetValue(entity, out var visual) ? visual : null;
        }

        public static void UnregisterItemVisual(Entity entity)
        {
            _itemVisuals.Remove(entity);
        }

        // ================================================================
        // ОЧИСТКА ВСЕГО
        // ================================================================
        public static void Clear()
        {
            _registry.Clear();
            _gridUiRegistry.Clear();
            _itemVisuals.Clear();
        }
    }
}


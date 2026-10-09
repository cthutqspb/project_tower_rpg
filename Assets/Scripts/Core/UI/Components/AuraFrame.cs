using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI.Components
{
    // 🦾 СИ-КАНOН: Обычный слепой контроллер вёрстки! Никаких контрактов ресиверов!
    public class AuraFrame
    {
        private Entity _boundEntity = Entity.Null;
        private StaticGrid _slotsGrid;

        public Entity BoundEntity => _boundEntity;
        public StaticGrid SlotsGrid => _slotsGrid;

        public AuraFrame(VisualElement aurasRoot, int maxSlots = 24)
        {
            if (aurasRoot == null) return;

            _slotsGrid = new StaticGrid(maxSlots, 1, 0);
            _slotsGrid.AddToClassList("aura-grid");
            aurasRoot.Add(_slotsGrid);
            
            Debug.Log($"🔮 [AuraFrame]: Сетка аур успешно вшита в ноду '{aurasRoot.name}' на {maxSlots} ячеек.");
        }

        public void BindToEntity(Entity entity)
        {
            Debug.Log($"[AuraFrame] Слепой BindToEntity: old={_boundEntity.Index}, new={entity.Index}");
            _boundEntity = entity;
            
            // Просто прокидываем сущность контейнера аур глубоко в автономный StaticGrid!
            // Сетка сама запишет себя в UIRegistry и сама шёлково примет буфер из ECS!
            _slotsGrid?.BindToEntity(entity);
        }
    }
}


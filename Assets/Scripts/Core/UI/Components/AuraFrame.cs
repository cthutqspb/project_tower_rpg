using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class AuraFrame : IEcsUiBufferReceiver< AuraSlot >
    {
        private Entity _boundEntity = Entity.Null;
        private StaticGrid _slotsGrid;

        public Entity BoundEntity => _boundEntity;
        public StaticGrid SlotsGrid => _slotsGrid;

        // 🦾 ХИРУРГИЧЕСКИЙ СИ-КОНСТРУКТОР (Копейка в копейку как HealthBar!):
        // Принимает готовую ноду из вёрстки UnitFrame.uxml и сразу вшивает туда слепую сетку
        public AuraFrame(VisualElement aurasRoot, int maxSlots = 8)
        {
            if (aurasRoot == null) return;

            // Рождаем девственно слепой StaticGrid
            _slotsGrid = new StaticGrid(maxSlots, 1, 0);
            
            // Навешиваем USS-класс сетки из AuraFrame.uss для аккуратного ММО-выравнивания 32x32
            _slotsGrid.AddToClassList("aura-slots-container");
            
            // Вливаем сетку в дерево VisualElement родителя
            aurasRoot.Add(_slotsGrid);
            
            Debug.Log($"🔮 [AuraFrame]: Сетка аур успешно вшита в ноду '{aurasRoot.name}' на {maxSlots} ячеек.");
        }

        public void BindToEntity(Entity entity)
        {
            if (_boundEntity != Entity.Null) UIRegistry.Unregister(_boundEntity, this);
            
            _boundEntity = entity;
            
            if (_boundEntity != Entity.Null)
            {
                UIRegistry.Register(_boundEntity, this);
                
                // Прокидываем слепую привязку сущности технического контейнера глубоко в сетку
                _slotsGrid?.BindToEntity(entity);
            }
        }

        // Выполняем жесткий контракт интерфейса IEcsUiBufferReceiver вслепую
        public void UpdateFromBuffer(
            DynamicBuffer< AuraSlot > buffer,
            bool isOnlyValidation = false,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer< ActiveCooldownElement > cooldowns = default
        )
        {
            // Напрямую скармливаем Си-байты аур нашему слепому StaticGrid!
            _slotsGrid?.UpdateFromBuffer(buffer, isOnlyValidation, gcdRemaining, gcdDuration, cooldowns);
        }
    }
}


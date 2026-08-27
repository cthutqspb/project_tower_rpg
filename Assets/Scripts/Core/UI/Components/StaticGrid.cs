using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement,
                              IEcsUiBufferReceiver<ItemSlot>,
                              IEcsUiBufferReceiver<ActionBarSlot>,
                              IEntityContainer
    {
        private int _columns;
        private int _rows;
        private int _startIndex;
        private List<SlotElement> _slots = new();
        private Entity _boundEntity;

        public Entity BoundEntity => _boundEntity;

        public StaticGrid(int columns, int rows,  int startIndex = 0)
        {
            _columns = columns;
            _rows = rows;
            _startIndex = startIndex;
            
            this.AddToClassList("static-grid-container");
            //this.AddToClassList($"grid-{gridType}");
            
            this.pickingMode = PickingMode.Ignore;
            this.style.width = columns * 48;
            this.style.flexDirection = FlexDirection.Row;
            this.style.flexWrap = Wrap.Wrap;
            this.style.flexShrink = 0;
            this.style.flexGrow = 0;

            for (int i = 0; i < columns * rows; i++)
            {
                int bufferSlotIndex = _startIndex + i;

                var slot = new SlotElement
                {
                    SlotIndex = bufferSlotIndex,
                    name = $"slot-{i}",
                    style =
                    {
                        width = 40,
                        height = 40,
                        marginTop = 2,
                        marginRight = 2,
                        marginBottom = 2,
                        marginLeft = 2,
                        backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f)
                    }
                };

                _slots.Add(slot);
                Add(slot);
            }
        }

        public void BindToEntity(Entity targetEntity)
        {
            Debug.Log($"[StaticGrid] BindToEntity для сущности: {targetEntity}");
            
            _boundEntity = targetEntity;
            
            foreach (var slot in _slots)
            {
                slot.ContainerEntity = targetEntity;
            }

            UIRegistry.Register(targetEntity, this);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            if (em.HasBuffer<ItemSlot>(targetEntity))
            {
                var slots = em.GetBuffer<ItemSlot>(targetEntity);
                UpdateFromBuffer(slots);
            }
            else
            {
                Debug.LogWarning($"[StaticGrid] Сущность {targetEntity} не имеет универсального буфера ItemSlot");
            }
        }

                // =========================================================================
        // ⚔️ ПОТОК ИНВЕНТАРЯ (Вызывается автоматически для DynamicBuffer<ItemSlot>)
        // =========================================================================
        public void UpdateFromBuffer(DynamicBuffer<ItemSlot> slots)
        {
            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} предметов инвентаря.");
            
            // Кристально чистая зачистка визуала экрана
            foreach (var slot in _slots)
            {
                slot.ClearVisual();
            }

            // Слепо и быстро выводим предметы. Никаких ContainerHelper и подсчетов банок!
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                _slots[i].SetData(itemId, config, i, slotData.Amount);
            }
        }

        // =========================================================================
        // 🔮 РЕЛЬСЫ ЭКШЕН-БАРА (Слепо и реактивно рендерит ВСЕ хоткеи 0..23)
        // =========================================================================
        public void UpdateFromBuffer(DynamicBuffer<ActionBarSlot> slots)
        {
            foreach (var slot in _slots) slot.ClearVisual();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            // 🔥 ИСПОЛЬЗУЕМ _startIndex ДЛЯ СМЕЩЕНИЯ
            for (int i = 0; i < _slots.Count && (i + _startIndex) < slots.Length; i++)
            {
                int absoluteIndex = i + _startIndex;
                var slotData = slots[absoluteIndex];
                var abilityId = slotData.AbilityId.ToString();
                if (string.IsNullOrEmpty(abilityId)) continue;

                var config = ItemsDatabase.GetItem(abilityId);
                int displayAmount = 1; 

                if (slotData.SlotType == "item")
                {
                    displayAmount = 0;
                    Entity invEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(_boundEntity, em);

                    if (invEntity != Entity.Null && em.HasBuffer<ItemSlot>(invEntity))
                    {
                        var inventorySlots = em.GetBuffer<ItemSlot>(invEntity);
                        for (int idx = 0; idx < inventorySlots.Length; idx++)
                        {
                            if (inventorySlots[idx].DataId == abilityId)
                            {
                                displayAmount += inventorySlots[idx].Amount;
                            }
                        }
                    }
                }

                // ✅ ПЕРЕДАЕМ ГЛОБАЛЬНЫЙ ИНДЕКС (absoluteIndex) ДЛЯ БИНДОВ
                _slots[i].SetData(abilityId, config, absoluteIndex, displayAmount, true);
            }
        }
    }
}

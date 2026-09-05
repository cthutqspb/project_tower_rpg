using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;

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
        public void UpdateFromBuffer(DynamicBuffer<ItemSlot> slots, bool isOnlyValidation = true)
        {
            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} предметов инвентаря.");
            
            foreach (var slot in _slots)
            {
                slot.ClearVisual();
            }

            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                _slots[i].SetData(itemId, i, slotData.Amount);
            }
        }

        // =========================================================================
        // 🔮 РЕЛЬСЫ ЭКШЕН-БАРА (Слепо и реактивно рендерит ВСЕ хоткеи 0..23)
        // =========================================================================
        public void UpdateFromBuffer(DynamicBuffer<ActionBarSlot> slots, bool isOnlyValidation = false)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            // ✅ ЕСЛИ ТОЛЬКО ВАЛИДАЦИЯ — НЕ ЧИСТИМ ВИЗУАЛ
            if (!isOnlyValidation)
            {
                foreach (var slot in _slots) slot.ClearVisual();
            }

            DynamicBuffer<ItemSlot> inventorySlots = default;
            bool hasValidInventory = false;

            // 🦾 СИ-ЗАЩИТА ОЗУ: Лезем в рюкзак СТРОГО в тяжелом потоке структуры!
            // Дополнительно страхуем чанки через легальный Си-флаг isReadOnly: true.
            if (!isOnlyValidation && _boundEntity != Entity.Null && em.Exists(_boundEntity) && em.HasComponent<BuffersLinkComponent>(_boundEntity))
            {
                Entity invEntity = em.GetComponentData<BuffersLinkComponent>(_boundEntity).Inventory;
                if (invEntity != Entity.Null && em.Exists(invEntity) && em.HasBuffer<ItemSlot>(invEntity))
                {
                    inventorySlots = em.GetBuffer<ItemSlot>(invEntity, isReadOnly: true);
                    hasValidInventory = true;
                }
            }

            for (int i = 0; i < _slots.Count && (i + _startIndex) < slots.Length; i++)
            {
                int absoluteIndex = i + _startIndex;
                var slotData = slots[absoluteIndex];
                var abilityId = slotData.AbilityId.ToString();
                if (string.IsNullOrEmpty(abilityId)) continue;

                // ✅ ВАЛИДАЦИЯ (всегда)
                var validationResult = AbilityValidator.CheckCastPossibility(abilityId, _boundEntity, em);

                // ✅ ЕСЛИ ТОЛЬКО ВАЛИДАЦИЯ — ОБНОВЛЯЕМ ТОЛЬКО ЦВЕТ/ПРОЗРАЧНОСТЬ
                // Процессор наглухо скипает весь тяжелый код ниже, инвентарь девственно чист!
                if (isOnlyValidation)
                {
                    _slots[i].SetValidation(validationResult);
                    continue;
                }

                // ✅ ПОЛНЫЙ РЕНДЕРИНГ (иконка, количество, бинд)
                int displayAmount = 1;

                if (slotData.SlotType == "item")
                {
                    displayAmount = 0;
                    if (hasValidInventory)
                    {
                        for (int idx = 0; idx < inventorySlots.Length; idx++)
                        {
                            if (inventorySlots[idx].DataId == slotData.AbilityId)
                            {
                                displayAmount += inventorySlots[idx].Amount;
                            }
                        }
                    }
                }

                string bindingText = slotData.KeyBinding.ToString();
                _slots[i].SetData(abilityId, absoluteIndex, displayAmount, true, bindingText, validationResult);
            }
        }
    }
}

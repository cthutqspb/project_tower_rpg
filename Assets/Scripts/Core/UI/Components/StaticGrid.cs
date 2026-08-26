using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement,
                              IEcsUiBufferReceiver<SlotData>,
                              IEcsUiBufferReceiver<ActionBarSlot>,
                              IEntityContainer
    {
        private int _columns;
        private int _rows;
        private string _gridType;
        private ContainerType _containerType;
        private int _panelIndex;
        private List<SlotElement> _slots = new();
        private Entity _boundEntity;

        public Entity BoundEntity => _boundEntity;

        public StaticGrid(int columns, int rows, string gridType, int panelIndex=0)
        {
            _columns = columns;
            _rows = rows;
            _gridType = gridType;
            _panelIndex = panelIndex;
            
            this.AddToClassList("static-grid-container");
            this.AddToClassList($"grid-{gridType}");
            
            this.pickingMode = PickingMode.Ignore;
            this.style.width = columns * 48;
            this.style.flexDirection = FlexDirection.Row;
            this.style.flexWrap = Wrap.Wrap;
            this.style.flexShrink = 0;
            this.style.flexGrow = 0;

            for (int i = 0; i < columns * rows; i++)
            {
                var slot = new SlotElement
                {
                    SlotIndex = i,
                    // ❌ GridType = _gridType, ← УДАЛИТЬ!
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
            if (em.HasBuffer<SlotData>(targetEntity))
            {
                var slots = em.GetBuffer<SlotData>(targetEntity);
                UpdateFromBuffer(slots);
            }
            else
            {
                Debug.LogWarning($"[StaticGrid] Сущность {targetEntity} не имеет универсального буфера SlotData");
            }
        }

        // =========================================================================
        // ⚔️ РЕЛЬСЫ ИНВЕНТАРЯ И КУКЛЫ (Обслуживает строго материальные предметы)
        // =========================================================================
        public void UpdateFromBuffer(DynamicBuffer<SlotData> slots)
        {
            // Если эта сетка — экшен-бар, мы сразу выходим, ей тут делать нечего!
            if (_gridType == "action-bar" || _gridType == "ACTION_BAR") return;

            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} универсальных слотов. Фильтр по строке: '{_gridType}'");
            
            foreach (var slot in _slots)
            {
                slot.ClearVisual();
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var slotData = slots[i];

                // Проверяем, что тип контейнера в буфере соответствует типу окна на экране ("inventory" или "paperdoll")
                if (slotData.ContainerType.ToString().ToLower() == _gridType.ToLower())
                {
                    int uiIndex = slotData.SlotIndex;
                    if (uiIndex >= 0 && uiIndex < _slots.Count)
                    {
                        var itemId = slotData.DataId.ToString();
                        var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                        
                        _slots[uiIndex].SetData(itemId, config, uiIndex, slotData.Amount);
                    }
                }
            }
        }

        // =========================================================================
        // 🔮 РЕЛЬСЫ ЭКШЕН-БАРА (Обслуживает строго ссылки и ярлыки в башке Игрока)
        // =========================================================================
        public void UpdateFromBuffer(DynamicBuffer<ActionBarSlot> slots)
        {
            // Если эта сетка — НЕ экшен-бар, сразу выходим наглухо!
            if (_gridType != "action-bar" && _gridType != "ACTION_BAR") return;

            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} ярлыков способностей. Панель: #{_panelIndex}");
            
            foreach (var slot in _slots)
            {
                slot.ClearVisual();
            }

            // Логика подсчета количества предметов из сумки (то, что мы набросали шагом ранее)
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            for (int i = 0; i < slots.Length; i++)
            {
                var slotData = slots[i];

                // Фильтруем элементы буфера строго по номеру этой панели хоткеев
                if (slotData.PanelIndex == _panelIndex)
                {
                    int targetUiIndex = slotData.SlotIndex; // Прямой локальный индекс экрана 0..11

                    if (targetUiIndex >= 0 && targetUiIndex < _slots.Count)
                    {
                        var abilityId = slotData.AbilityId.ToString();
                        if (string.IsNullOrEmpty(abilityId)) continue;

                        var config = ItemsDatabase.GetItem(abilityId);
                        int displayAmount = 1; 

                        // Динамический подсчет стака банок из рюкзака, если ярлык — это предмет
                        if (slotData.SlotType == "item")
                        {
                            displayAmount = 0;
                            Entity invEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(_boundEntity, em);

                            if (invEntity != Entity.Null && em.HasBuffer<SlotData>(invEntity))
                            {
                                var inventorySlots = em.GetBuffer<SlotData>(invEntity);
                                for (int idx = 0; idx < inventorySlots.Length; idx++)
                                {
                                    if (inventorySlots[idx].DataId == abilityId)
                                    {
                                        displayAmount += inventorySlots[idx].Amount;
                                    }
                                }
                            }
                        }

                        _slots[targetUiIndex].SetData(abilityId, config, targetUiIndex, displayAmount);
                    }
                }
            }
        }
    
    }
}

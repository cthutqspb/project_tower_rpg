using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement, IEcsUiBufferReceiver<SlotData>, IEntityContainer
    {
        private int _columns;
        private int _rows;
        private List<SlotElement> _slots = new();
        private Entity _boundEntity;

        public Entity BoundEntity => _boundEntity;

        public StaticGrid(int columns, int rows, string gridType)
        {
            _columns = columns;
            _rows = rows;
            
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

        public void UpdateFromBuffer(DynamicBuffer<SlotData> slots)
        {
            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} универсальных слотов");
            
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                // ✅ ИСПРАВЛЕНО: убрали gridType
                _slots[i].SetData(itemId, config, i, slotData.Amount);
            }
        }
    }
}

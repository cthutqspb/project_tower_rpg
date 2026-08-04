using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement, IDataSourceProvider, IEcsUiBufferReceiver<SlotData>
    {
        private string _gridType;
        private int _columns;
        private int _rows;
        private List<SlotElement> _slots = new();
        private Entity _inventoryEntity;

        public string DataSourceId { get; set; }
        public string GridType => _gridType;
        public Entity InventoryEntity => _inventoryEntity;

        public StaticGrid(int columns, int rows, string gridType)
        {
            _columns = columns;
            _rows = rows;
            _gridType = gridType;
            
            this.AddToClassList("static-grid-container");
            this.AddToClassList($"grid-{gridType}");
            this.pickingMode = PickingMode.Position;

            this.style.width = columns * 48;
            this.style.height = rows * 48;
            this.style.flexDirection = FlexDirection.Row;
            this.style.flexWrap = Wrap.Wrap;
            this.style.flexShrink = 0;
            this.style.flexGrow = 0;

            // Создаём слоты
            for (int i = 0; i < columns * rows; i++)
            {
                var slot = new SlotElement();
                slot.SlotIndex = i;
                slot.DataSourceId = DataSourceId;
                slot.GridType = _gridType;
                slot.Source = this;
                // slot.InventoryEntity будет задано позже в BindToEntity

                slot.style.width = 40;
                slot.style.height = 40;
                slot.style.marginTop = 2;
                slot.style.marginRight = 2;
                slot.style.marginBottom = 2;
                slot.style.marginLeft = 2;
                slot.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

                _slots.Add(slot);
                Add(slot);
            }
        }

        public void BindToEntity(Entity inventoryEntity)
        {
            Debug.Log($"[StaticGrid] BindToEntity: {inventoryEntity}");
            
            _inventoryEntity = inventoryEntity;
            
            // Обновляем InventoryEntity у всех слотов
            foreach (var slot in _slots)
            {
                slot.InventoryEntity = inventoryEntity;
            }
            
            UIRegistry.Register(inventoryEntity, this);
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            if (em.HasBuffer<SlotData>(inventoryEntity))
            {
                var slots = em.GetBuffer<SlotData>(inventoryEntity);
                UpdateFromBuffer(slots);
            }
            else
            {
                Debug.LogWarning($"[StaticGrid] InventoryEntity {inventoryEntity} не имеет буфера SlotData");
            }
        }

        public void UpdateFromBuffer(DynamicBuffer<SlotData> slots)
        {
            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} слотов");
            
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slot = slots[i];
                var itemId = slot.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                _slots[i].SetData(itemId, config, _gridType, i);
            }
        }

        string IDataSourceProvider.DataSourceId => DataSourceId;
    }
}

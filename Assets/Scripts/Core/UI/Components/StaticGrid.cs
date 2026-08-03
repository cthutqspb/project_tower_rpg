using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement, IDataSourceProvider
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

            for (int i = 0; i < columns * rows; i++)
            {
                var slot = new SlotElement();
                slot.SlotIndex = i;
                slot.DataSourceId = DataSourceId;
                slot.GridType = _gridType;
                slot.Source = this;
                slot.InventoryEntity = _inventoryEntity;

                slot.style.width = 40;
                slot.style.height = 40;
                slot.style.marginTop = 2;
                slot.style.marginRight = 2;
                slot.style.marginBottom = 2;
                slot.style.marginLeft = 2;
                slot.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);

                _slots.Add(slot);
                Add(slot);
            }
        }

        public void SetInventoryEntity(Entity inventoryEntity)
        {
            _inventoryEntity = inventoryEntity;
            foreach (var slot in _slots)
            {
                slot.InventoryEntity = inventoryEntity;
            }
            RefreshAll();
        }

        public void RefreshAll()
        {
            foreach (var slot in _slots)
            {
                slot.Refresh();
            }
        }

        public void RefreshSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].Refresh();
        }

        // ================================================================
        // 🖱️ ОБРАБОТЧИКИ КЛИКОВ
        // ================================================================

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            var slot = evt.currentTarget as SlotElement;
            if (slot == null) return;

            if (evt.button == 1) // ПКМ
            {
                Debug.Log($"ПКМ по слоту {slot.SlotIndex}");
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0) // ЛКМ
            {
                // ✅ Берём данные из ECS через слот
                string itemId = slot.GetItemId();
                int amount = slot.GetAmount();
                
                if (!string.IsNullOrEmpty(itemId))
                {
                    DragManager.Instance.StartDrag(
                        source: this,
                        slotIndex: slot.SlotIndex,
                        itemId: itemId,
                        amount: amount,
                        icon: null,
                        sourceId: DataSourceId,
                        gridType: _gridType
                    );
                    evt.StopPropagation();
                }
            }
        }

        private void OnSlotPointerUp(PointerUpEvent evt)
        {
            if (!DragManager.Instance.IsDragging) return;

            var slot = evt.currentTarget as SlotElement;
            if (slot == null) return;

            DragManager.Instance.Finish(this, slot.SlotIndex);
            evt.StopPropagation();
        }

        // ================================================================
        // IDataSourceProvider
        // ================================================================
        string IDataSourceProvider.DataSourceId => DataSourceId;
    }
}

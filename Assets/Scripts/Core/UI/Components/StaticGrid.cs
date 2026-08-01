using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement, IDataSourceProvider
    {
        private string _gridType;
        private int _columns;
        private int _rows;
        private List<SlotElement> _slots = new();

        public string DataSourceId { get; set; }
        public string GridType => _gridType;

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

        // ================================================================
        // 🔄 РЕАКТИВНОЕ ОБНОВЛЕНИЕ СЛОТА ИЗ ECS СИСТЕМЫ
        // ================================================================
        public void UpdateSlot(int index, string dataId, string dataType, int amount, string containerType)
        {
            if (index < 0 || index >= _slots.Count) return;

            var slot = _slots[index];
            
            // Если в ячейке ECS пусто — очищаем UI-слот
            if (string.IsNullOrEmpty(dataId) || amount <= 0)
            {
                slot.ClearSlot();
                return;
            }

            // Достаем конфиг предмета (для способностей/аур здесь добавится их база данных)
            ItemConfig config = null;
            if (dataType == "item" || containerType == "inventory")
            {
                config = ItemsDatabase.GetItem(dataId);
            }

            // Передаем плоские данные в UI-слот.
            // Убедись, что твой SlotElement принимает параметры в таком порядке или обнови его.
            slot.SetData(dataId, config, _gridType, index);
        }

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

            if (evt.button == 0 && !string.IsNullOrEmpty(slot.ItemId)) // ЛКМ
            {
                DragManager.Instance.StartDrag(
                    source: this,
                    slotIndex: slot.SlotIndex,
                    itemId: slot.ItemId,
                    amount: 1,
                    icon: null,
                    sourceId: DataSourceId,
                    gridType: _gridType
                );
                evt.StopPropagation();
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
    }
}


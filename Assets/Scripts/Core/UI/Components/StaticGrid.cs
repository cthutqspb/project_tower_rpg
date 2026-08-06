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
        private string _dataSourceId;

        // ИСПРАВЛЕНО: Нейтральное имя сущности. Подходит для инвентаря, экшенбаров и аур.
        private Entity _boundEntity;

        // Явно обновляем DataSourceId у слотов при его изменении снаружи
        public string DataSourceId 
        { 
            get => _dataSourceId; 
            set 
            {
                _dataSourceId = value;
                foreach (var slot in _slots)
                {
                    slot.DataSourceId = value;
                }
            }
        }
        
        public string GridType => _gridType;

        // ИСПРАВЛЕНО: Реализация интерфейсного свойства. То, что будет читать базовое окно UIWindow!
        public Entity BoundEntity => _boundEntity;

        public StaticGrid(int columns, int rows, string gridType)
        {
            _columns = columns;
            _rows = rows;
            _gridType = gridType;
            
            this.AddToClassList("static-grid-container");
            this.AddToClassList($"grid-{gridType}");
            
            // Сетка должна пропускать клики сквозь себя к слотам
            this.pickingMode = PickingMode.Ignore; 

            this.style.width = columns * 48;
            this.style.flexDirection = FlexDirection.Row;
            this.style.flexWrap = Wrap.Wrap;
            this.style.flexShrink = 0;
            this.style.flexGrow = 0;

            // Создаём слоты
            for (int i = 0; i < columns * rows; i++)
            {
                var slot = new SlotElement();
                slot.SlotIndex = i; 
                slot.GridType = _gridType;
                slot.name = $"slot-{i}";

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

        public void BindToEntity(Entity targetEntity)
        {
            Debug.Log($"[StaticGrid] BindToEntity для сущности: {targetEntity}");
            
            _boundEntity = targetEntity;
            
            // Обновляем Entity у всех вложенных дочерних слотов
            foreach (var slot in _slots)
            {
                slot.ContainerEntity = targetEntity;
            }
            
            // ИСПРАВЛЕНО: Строка UIRegistry.Register ОТСЮДА УДАЛЕНА! 
            // Теперь базовое окно UIWindow само регистрирует эту сетку при открытии.

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
                
                _slots[i].SetData(itemId, config, _gridType, i, slotData.Amount);
            }
        }

        string IDataSourceProvider.DataSourceId => DataSourceId;
    }
}


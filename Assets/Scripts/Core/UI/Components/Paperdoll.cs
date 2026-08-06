using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class Paperdoll : VisualElement, IDataSourceProvider, IEcsUiBufferReceiver<SlotData>
    {
        private string _dataSourceId;
        private Entity _paperdollEntity; // ИСПРАВЛЕНО: Перевели в приватное нейтральное поле
        
        private List<SlotElement> _slots = new();

        // ИСПРАВЛЕНО: Реализация интерфейсного свойства для автоматического сканирования базовым окном UIWindow
        public Entity BoundEntity => _paperdollEntity;

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
        
        public Paperdoll(VisualTreeAsset uxml)
        {
            this.AddToClassList("paperdoll-grid");
            
            if (uxml != null)
            {
                uxml.CloneTree(this);
                Debug.Log("[Paperdoll] UXML загружен");
            }
            else
            {
                Debug.LogError("[Paperdoll] UXML не передан!");
                return;
            }
            
            var leftColumn = this.Q<VisualElement>("left-column");
            var rightColumn = this.Q<VisualElement>("right-column");
            var bottomLeft = this.Q<VisualElement>("bottom-left");
            var bottomRight = this.Q<VisualElement>("bottom-right");

            // 0. HEAD (Левая колонка)
            if (leftColumn != null)
            {
                CreatePaperdollSlot(leftColumn, 0, "HEAD", "paperdoll-slot-HEAD");
                // 1. CHEST (Левая колонка)
                CreatePaperdollSlot(leftColumn, 1, "CHEST", "paperdoll-slot-CHEST");
                // 2. LEGS (Левая колонка)
                CreatePaperdollSlot(leftColumn, 2, "LEGS", "paperdoll-slot-LEGS");
            }

            // 3. MAIN_HAND (Нижняя левая колонка)
            if (bottomLeft != null)
            {
                CreatePaperdollSlot(bottomLeft, 3, "MAIN_HAND", "paperdoll-slot-MAIN_HAND");
                // 4. OFF_HAND (Нижняя левая колонка)
                CreatePaperdollSlot(bottomLeft, 4, "OFF_HAND", "paperdoll-slot-OFF_HAND");
            }
        }

        private void CreatePaperdollSlot(VisualElement parent, int index, string nameId, string ussClass)
        {
            var slot = new SlotElement();
            slot.SlotIndex = index; 
            slot.GridType = "paperdoll";
            slot.DataSourceId = DataSourceId;
            slot.ContainerEntity = _paperdollEntity;
            slot.name = $"slot-{nameId}";
            slot.AddToClassList(ussClass);
            
            slot.pickingMode = PickingMode.Position;

            _slots.Add(slot);
            parent.Add(slot);
        }
        
        public void BindToEntity(Entity paperdollEntity)
        {
            Debug.Log($"[Paperdoll] BindToEntity: {paperdollEntity}");
            _paperdollEntity = paperdollEntity;
            
            foreach (var slot in _slots)
            {
                slot.ContainerEntity = paperdollEntity;
            }
            
            // ИСПРАВЛЕНО: Саморегистрация удалена! UIWindow сделает всё сам.
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            if (em.HasBuffer<SlotData>(paperdollEntity))
            {
                var slots = em.GetBuffer<SlotData>(paperdollEntity);
                UpdateFromBuffer(slots);
            }
        }
        
        public void UpdateFromBuffer(DynamicBuffer<SlotData> slots)
        {
            Debug.Log($"[Paperdoll] UpdateFromBuffer: {slots.Length} слотов экипировки");
            
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                _slots[i].SetData(itemId, config, "paperdoll", i, slotData.Amount);
            }
        }
        
        string IDataSourceProvider.DataSourceId => DataSourceId;
    }
}


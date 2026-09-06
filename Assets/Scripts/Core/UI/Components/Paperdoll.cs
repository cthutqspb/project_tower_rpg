using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class Paperdoll : VisualElement, IEcsUiBufferReceiver<ItemSlot>, IEntityContainer
    {
        private Entity _paperdollEntity;
        private List<SlotElement> _slots = new();

        public Entity BoundEntity => _paperdollEntity;

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
            var bottomLeft = this.Q<VisualElement>("bottom-left");

            if (leftColumn != null)
            {
                CreatePaperdollSlot(leftColumn, 0, "HEAD", "paperdoll-slot-HEAD");
                CreatePaperdollSlot(leftColumn, 1, "CHEST", "paperdoll-slot-CHEST");
                CreatePaperdollSlot(leftColumn, 2, "LEGS", "paperdoll-slot-LEGS");
            }

            if (bottomLeft != null)
            {
                CreatePaperdollSlot(bottomLeft, 3, "MAIN_HAND", "paperdoll-slot-MAIN_HAND");
                CreatePaperdollSlot(bottomLeft, 4, "OFF_HAND", "paperdoll-slot-OFF_HAND");
            }
        }

        private void CreatePaperdollSlot(VisualElement parent, int index, string nameId, string ussClass)
        {
            var slot = new SlotElement
            {
                SlotIndex = index,
                ContainerEntity = _paperdollEntity,
                name = $"slot-{nameId}",
                pickingMode = PickingMode.Position
            };
            slot.AddToClassList(ussClass);

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
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            if (em.HasBuffer<ItemSlot>(paperdollEntity))
            {
                var slots = em.GetBuffer<ItemSlot>(paperdollEntity);
                UpdateFromBuffer(slots);
            }
        }
        
        public void UpdateFromBuffer(
            DynamicBuffer<ItemSlot> slots,
            bool isOnlyValidation = true,
            float gcdRemaining = 0f,
            float gcdDuration = 0f
        )
        {
            Debug.Log($"[Paperdoll] UpdateFromBuffer: {slots.Length} слотов экипировки");
            
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                // ✅ ИСПРАВЛЕНО: убрали gridType
                _slots[i].SetData(itemId, i, slotData.Amount);
            }
        }
    }
}

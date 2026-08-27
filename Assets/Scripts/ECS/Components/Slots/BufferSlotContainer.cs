using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

public class BufferSlotContainer : ISlotContainer
{
    private Entity _entity;
    private BufferLookup<ItemSlot> _slotLookup;

    public Entity Entity => _entity;

    public BufferSlotContainer(Entity entity, BufferLookup<ItemSlot> slotLookup)
    {
        _entity = entity;
        _slotLookup = slotLookup;
    }

    public bool HasContent(int slot)
    {
        var slots = _slotLookup[_entity];
        
        // ВРЕМЕННЫЙ ПРИНТ ДЛЯ ОТЛАДКИ КУКЛЫ:
        if (slots[slot].ContainerType == ContainerType.PAPERDOLL)
        {
            Debug.Log($"[BufferSlotContainer] Проверка слота куклы #{slot}. Предмет в ECS: '{slots[slot].DataId}', Пустой? {slots[slot].IsEmpty}, Длина буфера: {slots.Length}");
        }

        if (slot < 0 || slot >= slots.Length) return false;
        return !slots[slot].IsEmpty;
    }

    public object GetContent(int slot)
    {
        var slots = _slotLookup[_entity];
        if (slot < 0 || slot >= slots.Length) return null;
        return slots[slot];
    }

    public void SetContent(int slot, object content)
    {
        var slots = _slotLookup[_entity];
        if (slot < 0 || slot >= slots.Length) return;

        if (content is ItemSlot data)
        {
            // 🦾 ЗРЯЧЕE ОБНОВЛЕНИЕ ЧАНКА ПАМЯТИ ECS:
            // Берем текущую физическую ячейку, которая прямо сейчас выделена в ОЗУ
            var targetSlot = slots[slot];
            
            // Заменяем в ней ТОЛЬКО контент!
            targetSlot.DataId = data.DataId;
            targetSlot.DataType = data.DataType;
            targetSlot.Amount = data.Amount;
            
            // 🚨 ПАСПОРТ СЛОТА (ContainerType и EquipSlot) ОСТАЮТСЯ НЕПРИКОСНОВЕННЫМИ ДЛЯ КУКЛЫ!
            slots[slot] = targetSlot; // Применяем изменения обратно в буфер
        }
    }

    public int FindEmptySlot()
    {
        var slots = _slotLookup[_entity];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                return i;
            }
        }
        return -1;
    }

    public void ClearSlot(int slot)
    {
        var slots = _slotLookup[_entity];
        if (slot >= 0 && slot < slots.Length)
        {
            var originalEquipSlot = slots[slot].EquipSlot;
            slots[slot] = new ItemSlot
            {
                SlotIndex = slot,
                ContainerType = slots[slot].ContainerType,
                DataId = "",
                DataType = "",
                Amount = 0,
                EquipSlot = originalEquipSlot
            };
        }
    }

    public bool CanPlaceContent(int slot, object content)
    {
        var slots = _slotLookup[_entity];
        if (slot < 0 || slot >= slots.Length) return false;

        if (content is ItemSlot incomingData)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // =========================================================================
            // 🔥 ЗРЯЧАЯ ЗАЩИТА ОТ ВЛОЖЕННЫХ КОНТЕЙНЕРОВ (ГЛУБИНА = 1 по канону BG3)
            // =========================================================================
            // Если этот конкретный буфер ОЗУ принадлежит внешней бочке/мешку (ContainerTag),
            // И игрок пытается запихать внутрь неё ЕЩЁ ОДИН контейнер (мешок в бочку) -> БЛОКИРУЕМ!
            if (em.HasComponent<ContainerTag>(_entity) && IsContainerItem(incomingData.DataId.ToString()))
            {
                Debug.Log($"[BufferSlotContainer] Блокировка: нельзя класть контейнер '{incomingData.DataId}' внутрь другого контейнера!");
                return false;
            }

            var targetContainerType = slots[slot].ContainerType;

            switch (targetContainerType)
            {
                case ContainerType.INVENTORY:
                    return true; // В личный рюкзак Игрока (InventoryTag) бочки теперь будут лететь со свистом!

                case ContainerType.PAPERDOLL:
                    if (incomingData.IsEmpty) return false;
                    var config = ItemsDatabase.GetItem(incomingData.DataId.ToString());
                    if (config == null) return false;
                    return config.properties.equip_slot == slots[slot].EquipSlot.ToString();

                default:
                    return false;
            }
        }

        return false;
    }
 

    // ================================================================
    // 🛠️ ВСПОМОГАТЕЛЬНЫЙ МЕТОД
    // ================================================================
    private bool IsContainerItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        var config = ItemsDatabase.GetItem(itemId);
        return config != null && config.identity.type == "container";
    }
}

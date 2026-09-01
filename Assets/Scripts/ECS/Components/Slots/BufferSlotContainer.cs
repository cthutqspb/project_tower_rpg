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
        if (slot < 0 || slot >= slots.Length) return false;

        if (slots[slot].ContainerType == ContainerType.PAPERDOLL)
        {
            Debug.Log($"[BufferSlotContainer] Проверка слота куклы #{slot}. Предмет in ECS: '{slots[slot].DataId}', Пустой? {slots[slot].IsEmpty}, Длина буфера: {slots.Length}");
        }

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
            var targetSlot = slots[slot];
            targetSlot.SetContent(data);
            slots[slot] = targetSlot;
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
        if (slot < 0 || slot >= slots.Length) return;

        var targetSlot = slots[slot];
        targetSlot.ClearContent();
        slots[slot] = targetSlot;

        Debug.Log($"[BufferSlotContainer]: Точечно очищен контент слота #{slot}. Паспорт ячейки и анатомия '{targetSlot.EquipSlot}' неприкосновенны.");
    }

    public bool CanPlaceContent(int slot, object content)
    {
        var slots = _slotLookup[_entity];
        if (slot < 0 || slot >= slots.Length) return false;

        if (content is ItemSlot incomingData)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (em.HasComponent<ContainerTag>(_entity) && IsContainerItem(incomingData.DataId.ToString()))
            {
                Debug.Log($"[BufferSlotContainer] Блокировка: нельзя класть контейнер '{incomingData.DataId}' внутрь другого контейнера!");
                return false;
            }

            switch (slots[slot].ContainerType)
            {
                case ContainerType.INVENTORY:
                    return true;

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

    private bool IsContainerItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        var config = ItemsDatabase.GetItem(itemId);
        return config != null && config.identity.type == "container";
    }
}

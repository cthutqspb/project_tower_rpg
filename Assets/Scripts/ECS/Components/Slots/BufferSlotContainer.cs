using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

public class BufferSlotContainer : ISlotContainer
{
    private Entity _entity;
    private BufferLookup<SlotData> _slotLookup;

    public Entity Entity => _entity;

    public BufferSlotContainer(Entity entity, BufferLookup<SlotData> slotLookup)
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

        if (content is SlotData data)
        {
            data.SlotIndex = slot;
            slots[slot] = data;
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
            slots[slot] = new SlotData
            {
                SlotIndex = slot,
                ContainerType = slots[slot].ContainerType,
                DataId = "",
                DataType = "",
                Amount = 0,
                EquipSlot = EquipSlot.NONE
            };
        }
    }

    public bool CanPlaceContent(int slot, object content)
    {
        var slots = _slotLookup[_entity];
        if (slot < 0 || slot >= slots.Length) return false;

        if (content is SlotData incomingData)
        {
            var targetContainerType = slots[slot].ContainerType;

            switch (targetContainerType)
            {
                case ContainerType.INVENTORY:
                    return true;

                case ContainerType.PAPERDOLL:
                    if (incomingData.IsEmpty) return false;
                    var config = ItemsDatabase.GetItem(incomingData.DataId.ToString());
                    if (config == null) return false;
                    return config.properties.equip_slot == slots[slot].EquipSlot.ToString();

                case ContainerType.ACTION_BAR:
                    return incomingData.DataType == "ability" || incomingData.DataType == "item";

                default:
                    return false;
            }
        }

        return false;
    }
}

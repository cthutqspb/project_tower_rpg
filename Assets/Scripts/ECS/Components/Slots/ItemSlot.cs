using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct ItemSlot : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes DataId;   // ID предмета ("iron_sword")
        public FixedString64Bytes DataType; // "item", "spell"
        public int Amount;

        public Entity ItemEntity;
        
        // МЕНЯЕМ СТРОКИ НА ENUMS:
        public EquipSlot EquipSlot;
        public ContainerType ContainerType;
        
        public bool IsEmpty => DataId.IsEmpty || Amount <= 0;

        // 🦾 ТОЧЕЧНОЕ САМООЧИЩЕНИЕ: Паспорт и анатомия куклы ЗАЦЕМЕНТИРОВАНЫ!
        public void ClearContent()
        {
            DataId = "";
            DataType = "";
            Amount = 0;
            ItemEntity = Entity.Null;
        }

        // 🦾 ТОЧЕЧНЫЙ СЕТТЕР КОНТЕНТА: Заменяет строго живые Си-байты
        public void SetContent(object content)
        {
            if (content is ItemSlot data)
            {
                DataId = data.DataId;
                DataType = data.DataType;
                Amount = data.Amount;
                ItemEntity = data.ItemEntity;
            }
        }
    }
}


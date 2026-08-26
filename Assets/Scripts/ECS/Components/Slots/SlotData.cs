using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct SlotData : IBufferElementData
    {
        public int SlotIndex;
        public int PanelIndex;
        public FixedString64Bytes DataId;   // ID предмета ("iron_sword")
        public FixedString64Bytes DataType; // "item", "spell"
        public int Amount;
        
        // МЕНЯЕМ СТРОКИ НА ENUMS:
        public EquipSlot EquipSlot;
        public ContainerType ContainerType;
        
        public bool IsEmpty => DataId.IsEmpty || Amount <= 0;
    }
}


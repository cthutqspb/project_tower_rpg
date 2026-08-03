using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct SlotData : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes DataId;   // ← СТРОКА
        public FixedString64Bytes DataType;
        public int Amount;
        public FixedString64Bytes EquipSlot;
        public FixedString64Bytes ContainerType;
        
        public bool IsEmpty => DataId.IsEmpty || Amount <= 0;
    }
}

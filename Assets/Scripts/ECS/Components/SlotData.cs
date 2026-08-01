using Unity.Collections;
using Unity.Entities;
using Unity.Properties; // ← ДОБАВИТЬ для Unity 6 Data Binding

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag] // ← ДОБАВИТЬ: генерирует свойства для UI Toolkit
    public struct SlotData : IBufferElementData
    {
        public int SlotIndex; // ← ДОБАВИТЬ: индекс ячейки в сетке
        public FixedString64Bytes DataId;
        public FixedString64Bytes DataType;
        public int Amount;
        public FixedString64Bytes EquipSlot;
        public FixedString64Bytes ContainerType; // "inventory", "actionbar", "aura"
        
        public bool IsEmpty => string.IsNullOrEmpty(DataId.ToString()) || Amount <= 0;
    }
}


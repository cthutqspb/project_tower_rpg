using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct ActionBarSlot : IBufferElementData
    {
        public int SlotIndex;    // 0..11 локальный индекс на панели
        
        public FixedString64Bytes AbilityId; // "frostbolt", "melee_attack" или "crystal_sword"
        public FixedString64Bytes SlotType;  // "spell" или "item"
        
        public bool IsEmpty => AbilityId.IsEmpty;
    }
}


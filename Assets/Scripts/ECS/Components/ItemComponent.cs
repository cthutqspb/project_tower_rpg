using Unity.Entities;
using Unity.Collections;  // ← для FixedString64Bytes

namespace ProjectTowerRpg.ECS.Components
{
    public struct ItemComponent : IComponentData
    {
        public int Uid;
        public FixedString64Bytes ItemId;      // ← строка
        public int Amount;
        public FixedString64Bytes LootTableId; // ← строка
        public bool IsLooted;
    }
}

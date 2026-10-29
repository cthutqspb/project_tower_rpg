using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.Core.Units;

namespace ProjectTowerRpg.ECS.Components
{
    public struct UnitComponent : IComponentData
    {
        public FixedString64Bytes Uid;       // "player" или "skeleton_123"
        public FixedString64Bytes UnitId;    // "player_mage", "skeleton_warrior",
        public FixedString64Bytes NameKey;
        public FixedString64Bytes UniqueNameKey;
        public int Level;                    // 1, 5, 10...
        public FixedString64Bytes LootTableId;
        public FactionId Faction;
        public float AggroRadius;

        public FixedString32Bytes Race;
        public FixedString32Bytes Type;
        public FixedString32Bytes DefaultRank;
    }
}

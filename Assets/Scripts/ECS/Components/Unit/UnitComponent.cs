using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    public struct UnitComponent : IComponentData
    {
        public FixedString64Bytes Uid;       // "player" или "skeleton_123"
        public FixedString64Bytes UnitId;    // "player_mage", "skeleton_warrior",
        public FixedString64Bytes NameKey;
        public int Level;                    // 1, 5, 10...
        public float3 Position;              // позиция в мире
    }
}

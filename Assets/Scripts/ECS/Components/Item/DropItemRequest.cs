using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct DropItemRequest : IComponentData
    {
        public FixedString64Bytes ItemId;   // ← СТРОКА
        public int Amount;
        public float3 Position;
    }
}

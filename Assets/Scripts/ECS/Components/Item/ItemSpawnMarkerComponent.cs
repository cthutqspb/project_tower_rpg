using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct ItemSpawnMarkerComponent : IComponentData
    {
        public FixedString32Bytes ItemId;
        public int Amount;
        public float3 SpawnPosition;
        public FixedString64Bytes LootTableId;
        public int RespawnTime;
    }
}

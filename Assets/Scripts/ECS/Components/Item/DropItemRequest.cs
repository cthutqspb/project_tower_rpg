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

        public FixedString32Bytes LootTableId;
        public bool IsLooted;    // Была ли уже запущена генерация лута (чтобы не роллить повторно!)
        public bool IsCollected; // Забран ли контейнер в сумку (для контекстного меню и сейвов)
        public int RespawnTime;
    }
}

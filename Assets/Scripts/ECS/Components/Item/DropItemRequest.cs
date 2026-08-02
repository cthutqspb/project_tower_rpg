using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Временный компонент-запрос на спавн предмета в мире.
    /// Создается в экшене, обрабатывается в ItemSpawnSystem.
    /// </summary>
    public struct DropItemRequest : IComponentData
    {
        public FixedString32Bytes ItemId; // Строка из UI ("iron_sword")
        public int Amount;
        public float3 Position;           // Координаты падения куба
    }
}


using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    public class UnitAuthoring : MonoBehaviour
    {
        public float moveSpeed = 5.0f;
        public bool isPlayer = false;
    }

    public class UnitBaker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // ================================================================
            // ДВИЖЕНИЕ
            // ================================================================
            AddComponent(entity, new MovementComponent
            {
                speed = authoring.moveSpeed,
                direction = Unity.Mathematics.float3.zero
            });

            // ================================================================
            // ИГРОК (маркер)
            // ================================================================
            if (authoring.isPlayer)
            {
                AddComponent<PlayerTag>(entity);
            }

            // ================================================================
            // ❌ ИНВЕНТАРЬ НЕ СОЗДАЁМ! (только в UnitSpawnSystem)
            // ================================================================
        }
    }
}

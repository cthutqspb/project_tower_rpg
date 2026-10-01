using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    // Классический Монобех-паспорт для инспектора Unity
    public class ProjectileAuthoring : MonoBehaviour
    {
        [Header("Projectile Base Parameters")]
        public float MovementSpeed = 18f;
    }

    // 🦾 СИ-ЗАПЕКАТЕЛЬ: Unity 6 автоматически вызовет этот класс при старте игры,
    // уничтожит GameObject куба и превратит его в чистые ECS-компоненты в ОЗУ!
    public class ProjectileBaker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Навешиваем плоские структуры данных на префаб
            AddComponent<ProjectileTag>(entity);
            
            AddComponent(entity, new ProjectileMovement
            {
                Speed = authoring.MovementSpeed
            });
        }
    }
}


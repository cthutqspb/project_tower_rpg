using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core
{
    public static class PlayerUtils
    {
        public static bool TryGetPosition(out float3 position)
        {
            position = float3.zero;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return false;
            }

            var em = world.EntityManager;
            if (em == null)
            {
                return false;
            }

            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            if (query.IsEmpty)
            {
                return false;
            }

            var player = query.GetSingletonEntity();
            position = em.GetComponentData<LocalTransform>(player).Position;
            return true;
        }

        public static float3 GetPosition()
        {
            TryGetPosition(out var position);
            return position;
        }
    }
}

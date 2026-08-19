using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core
{
    public static class PlayerUtils
    {
        /// <summary>
        /// Универсальный метод получения сущности по тегу
        /// </summary>
        public static Entity GetEntityByTag<T>(EntityManager em = default) where T : unmanaged, IComponentData
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return Entity.Null;
            
            var entityManager = em == default ? world.EntityManager : em;
            var query = entityManager.CreateEntityQuery(typeof(T));
            
            if (query.IsEmpty)
                return Entity.Null;
            
            var entities = query.ToEntityArray(Allocator.Temp);
            var entity = entities.Length > 0 ? entities[0] : Entity.Null;
            entities.Dispose();
            
            return entity;
        }

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

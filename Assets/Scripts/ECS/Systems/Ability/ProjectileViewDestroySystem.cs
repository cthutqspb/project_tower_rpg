using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Presentation;

using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ProjectileViewDestroySystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private readonly System.Collections.Generic.List<Entity> _deadEntities = new();

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        protected override void OnUpdate()
        {
            if (_playerQuery.IsEmpty) return;

            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            float3 playerPosition = em.GetComponentData<LocalTransform>(
                _playerQuery.GetSingletonEntity()).Position;

            _deadEntities.Clear();

            CollectDeadProjectiles(em, playerPosition);
            DestroyDeadProjectiles(em, ecb);
        }

        // Проход 1: только читаем реестр, ничего не удаляем.
        private void CollectDeadProjectiles(EntityManager em, float3 playerPosition)
        {
            foreach (var entity in ProjectileViewRegistry.GetEntities())
            {
                // Сущность стёрта сервером — снаряд сделал импакт.
                if (!em.Exists(entity))
                {
                    _deadEntities.Add(entity);
                    continue;
                }

                // Снаряд жив, но улетел в туман войны.
                float3 pos = em.GetComponentData<LocalTransform>(entity).Position;
                if (math.distance(playerPosition, pos) > 55f)
                {
                    _deadEntities.Add(entity);
                }
            }
        }

        // Проход 2: уничтожаем. Реестр уже не итерируется.
        private void DestroyDeadProjectiles(EntityManager em, EntityCommandBuffer ecb)
        {
            for (int i = 0; i < _deadEntities.Count; i++)
            {
                Entity entity = _deadEntities[i];

                if (!ProjectileViewRegistry.TryGet(entity, out var sync)) continue;

                bool isDestroyed = !em.Exists(entity);

                Debug.Log(
                    $"🧹 [ProjectileViewDestroy]: Сношу 3D-тело снаряда " +
                    $"'{sync.gameObject.name}' (Импакт: {isDestroyed})");

                Object.Destroy(sync.gameObject);
                ProjectileViewRegistry.Unregister(entity);

                // Если снаряд ещё жив — снимаем VisualizedTag, чтобы мог заматериализоваться снова.
                if (!isDestroyed)
                    ecb.RemoveComponent<VisualizedTag>(entity);
            }
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

using Object = UnityEngine.Object;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ProjectileViewDestroySystem : SystemBase
    {
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _playerQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly< PlayerTag >(), 
                ComponentType.ReadOnly< LocalTransform >()
            );
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton< EndSimulationEntityCommandBufferSystem.Singleton >().CreateCommandBuffer(World.Unmanaged);

            if (_playerQuery.IsEmpty) return;
            var playerEntity = _playerQuery.GetSingletonEntity();
            float3 playerPosition = em.GetComponentData< LocalTransform >(playerEntity).Position;

            // 🚀 Выгребаем со сцены Unity только скрипты слежения снарядов!
            var projectileViews = Object.FindObjectsByType<SyncTransformWithEntity>(FindObjectsInactive.Exclude);
            if (projectileViews.Length == 0) return;

            foreach (var sync in projectileViews)
            {
                if (sync == null) continue;
                Entity boundEntity = sync.BoundEntity;

                // 🪦 КЕЙС 1: ECS-сущность снаряда стёрта сервером (Произошел ИМПАКТ!)
                bool isEntityDestroyed = !em.Exists(boundEntity);

                // 🌐 КЕЙС 2: Снаряд еще жив, но улетел в туман войны дальше 55 метров от игрока
                bool isTooFar = !isEntityDestroyed && math.distance(playerPosition, em.GetComponentData< LocalTransform >(boundEntity).Position) > 55f;

                if (isEntityDestroyed || isTooFar)
                {
                    Debug.Log($"🧹 [ProjectileViewDestroy]: Сношу 3D-тело снаряда '{sync.gameObject.name}' (Импакт: {isEntityDestroyed} | Куллинг: {isTooFar})");
                    
                    // Насильно стираем куб с экрана, очищая Mono-кучу
                    Object.Destroy(sync.gameObject);

                    // Если куб стерт по дистанции, но на сервере еще летит — снимаем флаг, чтобы перематериализовать позже
                    if (isTooFar)
                    {
                        ecb.RemoveComponent< VisualizedTag >(boundEntity);
                    }
                }
            }
        }
    }
}


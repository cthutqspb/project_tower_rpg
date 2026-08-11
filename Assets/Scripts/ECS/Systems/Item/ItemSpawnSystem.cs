using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine; // 🌟 Обязательно добавляем для Physics.Raycast
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemSpawnSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();

            foreach (var (request, requestEntity) in 
                     SystemAPI.Query<RefRO<DropItemRequest>>().WithEntityAccess())
            {
                Entity worldItemEntity = ecb.CreateEntity();

                string itemId = request.ValueRO.ItemId.ToString();
                
                // 🌟 МАТЕМАТИКА ПРИЖАТИЯ ПРЕДМЕТА К ТЕРРЕЙНУ:
                // Берем позицию дропа (обычно это координаты игрока)
                // Берём позицию из запроса дропа
                float3 spawnPosition = request.ValueRO.Position;

                // 🌟 Вызываем нашу утилиту в один клик!
                spawnPosition.y = PhysicsUtils.GetGroundHeight(spawnPosition);

                // Всё! Дальше твой чистый ECS код спавна компонента и LocalTransform
                ecb.AddComponent(worldItemEntity, LocalTransform.FromPosition(spawnPosition));

                ecb.AddComponent(worldItemEntity, new ItemComponent
                {
                    Uid = $"i_{(int)spawnPosition.x}_{(int)spawnPosition.z}".GetHashCode(),
                    ItemId = itemId,  
                    Amount = request.ValueRO.Amount,
                    LootTableId = "empty",
                    IsLooted = false
                });

                // Спавним ECS-сущность предмета с уже ИСПРАВЛЕННОЙ высотой Y на земле!
                ecb.AddComponent(worldItemEntity, LocalTransform.FromPosition(spawnPosition));

                Debug.Log($"[ItemSpawnSystem] Предмет успешно приземлен на холм. ItemId={itemId}, Y={spawnPosition.y}");

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}


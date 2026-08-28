using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
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
                // 1. Рождаем чистую ECS-сущность в памяти
                Entity itemEntity = ecb.CreateEntity();

                string itemIdStr = request.ValueRO.ItemId.ToString();
                float3 spawnPosition = request.ValueRO.Position;
                spawnPosition.y = PhysicsUtils.GetGroundHeight(spawnPosition);

                string generatedUidStr = $"i_{(int)spawnPosition.x}_{(int)spawnPosition.z}";
                int generatedUidHash = generatedUidStr.GetHashCode();

                // 2. Накатываем базовые unmanaged-компоненты в ОЗУ симуляции
                ecb.AddComponent(itemEntity, LocalTransform.FromPosition(spawnPosition));
                ecb.AddComponent(itemEntity, new ItemComponent
                {
                    Uid = generatedUidHash,
                    ItemId = itemIdStr,  
                    Amount = request.ValueRO.Amount,
                    LootTableId = request.ValueRO.LootTableId,
                    IsLooted = request.ValueRO.IsLooted,
                    RespawnTime = request.ValueRO.RespawnTime
                });

                Debug.Log($"[ItemSpawnSystem] Чистая ECS-сущность предмета родилась в ОЗУ: {itemIdStr}, Uid Hash={generatedUidHash}");

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}


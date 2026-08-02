using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // ВАЖНО: Заставляем систему жить только в игровом мире симуляции!
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

                int itemHashId = request.ValueRO.ItemId.GetHashCode();
                int emptyLootTableHash = "empty".GetHashCode();

                ecb.AddComponent(worldItemEntity, new ItemComponent
                {
                    Uid = $"i_{(int)request.ValueRO.Position.x}_{(int)request.ValueRO.Position.z}".GetHashCode(),
                    ItemId = itemHashId,
                    Amount = request.ValueRO.Amount,
                    LootTableId = emptyLootTableHash,
                    IsLooted = false
                });

                ecb.AddComponent(worldItemEntity, LocalTransform.FromPosition(request.ValueRO.Position));

                Debug.Log($"[ItemSpawnSystem] Сущность создана в ИГРОВОМ ОЗУ. ItemHash={itemHashId}");

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}


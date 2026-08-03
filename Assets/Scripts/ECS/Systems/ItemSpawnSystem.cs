using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;    // ← ДОБАВИТЬ для FixedString
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

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

                // ✅ Используем строку напрямую
                string itemId = request.ValueRO.ItemId.ToString();

                ecb.AddComponent(worldItemEntity, new ItemComponent
                {
                    Uid = $"i_{(int)request.ValueRO.Position.x}_{(int)request.ValueRO.Position.z}".GetHashCode(),
                    ItemId = itemId,  // ← строка
                    Amount = request.ValueRO.Amount,
                    LootTableId = "empty",
                    IsLooted = false
                });

                ecb.AddComponent(worldItemEntity, LocalTransform.FromPosition(request.ValueRO.Position));

                Debug.Log($"[ItemSpawnSystem] Сущность создана. ItemId={itemId}");

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}

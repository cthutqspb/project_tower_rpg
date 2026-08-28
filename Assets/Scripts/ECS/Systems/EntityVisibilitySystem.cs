using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class EntityVisibilitySystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            if (_playerQuery.IsEmpty) return;

            var ecb = _ecbSystem.CreateCommandBuffer();
            
            var playerEntity = _playerQuery.GetSingletonEntity();
            var playerTransform = EntityManager.GetComponentData<LocalTransform>(playerEntity);
            float3 playerPos = playerTransform.Position;

            // =========================================================================
            // 🧱 КЕЙС А: СПАВН ГРАФИКИ (Предмет свободно лежит в мире и игрок рядом)
            // =========================================================================
            foreach (var (transform, itemData, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ItemComponent>>()
                     .WithNone<StoredTag>()     // 🦾 ГВАРД: Предмет НЕ в инвентаре/сундуке (он на земле)!
                     .WithNone<VisualizedTag>() // ... и для него еще нет 3D-модели!
                     .WithEntityAccess())
            {
                float3 itemPos = transform.ValueRO.Position;
                if (math.distance(playerPos, itemPos) <= 50f)
                {
                    var universalPrefab = Resources.Load<GameObject>("Items/default_item");
                    if (universalPrefab != null)
                    {
                        var spawnedModel = Object.Instantiate(universalPrefab, itemPos, Quaternion.identity);
                        
                        string generatedUidStr = $"i_{(int)itemPos.x}_{(int)itemPos.z}";
                        string itemIdStr = itemData.ValueRO.ItemId.ToString();
                        spawnedModel.name = $"{itemIdStr}_{generatedUidStr}";

                        var view = spawnedModel.GetComponent<ItemView>();
                        if (view != null)
                        {
                            view.uid = generatedUidStr;
                            view.itemId = itemIdStr;
                            view.IsLinked = true;
                            view.Entity = entity;
                        }

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }

            // =========================================================================
            // 🧱 КЕЙС Б: ТЕХНИЧЕСКИЙ КУЛЛИНГ ДИСТАНЦИИ (Игрок просто убежал далеко)
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithNone<StoredTag>() // Только для тех, кто всё еще свободно лежит в мире
                     .WithAll<VisualizedTag>() 
                     .WithEntityAccess())
            {
                if (math.distance(playerPos, transform.ValueRO.Position) > 55f)
                {
                    var views = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
                    foreach (var view in views)
                    {
                        if (view.Entity == entity)
                        {
                            Object.Destroy(view.gameObject);
                            break;
                        }
                    }
                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }
        }
    }
}


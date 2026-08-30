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
            // 🎒 [РАЗДЕЛ ПРЕДМЕТОВ] КЕЙС А: СПАВН ГРАФИКИ ПРЕДМЕТОВ
            // =========================================================================
            foreach (var (transform, itemData, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ItemComponent>>()
                     .WithNone<StoredTag>()     
                     .WithNone<VisualizedTag>() 
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
            // 🎒 [РАЗДЕЛ ПРЕДМЕТОВ] КЕЙС Б: КУЛЛИНГ ДИСТАНЦИИ ПРЕДМЕТОВ
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithNone<StoredTag>() 
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

            // =========================================================================
            // 💀 [РАЗДЕЛ ЮНИТОВ] КЕЙС А: МАТЕРИАЛИЗАЦИЯ МОНСТРОВ / NPC / ИГРОКА
            // =========================================================================
            foreach (var (transform, unitData, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<UnitComponent>>()
                     .WithNone<VisualizedTag>() 
                     .WithEntityAccess())
            {
                float3 unitPos = transform.ValueRO.Position;
                if (math.distance(playerPos, unitPos) <= 50f)
                {
                    string unitIdStr = unitData.ValueRO.UnitId.ToString();
                    var unitPrefab = Resources.Load<GameObject>($"Units/{unitIdStr}");
                    
                    if (unitPrefab == null)
                    {
                        unitPrefab = Resources.Load<GameObject>("Units/default_unit");
                    }

                    if (unitPrefab != null)
                    {
                        var spawnedModel = Object.Instantiate(unitPrefab, unitPos, Quaternion.identity);
                        
                        string currentUid = unitData.ValueRO.Uid.ToString();
                        bool isPlayerEntity = EntityManager.HasComponent<PlayerTag>(entity);
                        spawnedModel.name = $"{unitIdStr}_{(isPlayerEntity ? "player" : currentUid)}";

                        var view = spawnedModel.GetComponent<UnitView>();
                        if (view != null)
                        {
                            view.uid = currentUid;
                            view.unitId = unitIdStr;
                            view.IsLinked = true;
                            view.entity = entity; 
                        }

                        var syncTransform = spawnedModel.GetComponent<SyncTransformWithEntity>();
                        if (syncTransform != null)
                        {
                            syncTransform.Initialize(entity);
                        }

                        // 🎥 🦾 АВТО-ПРИВЯЗКА КАМЕРЫ ДЛЯ ИГРОКА ПРИ МАТЕРИАЛИЗАЦИИ:
                        // Перенесено из фабрики! Камера мягко подхватит визуал героя, 
                        // как только он появится на экране симуляции!
                        if (isPlayerEntity)
                        {
                            var orbitCam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                            if (orbitCam != null)
                            {
                                orbitCam.Follow = spawnedModel.transform;
                                orbitCam.LookAt = spawnedModel.transform;
                                Debug.Log("🎥 [VisibilitySystem]: Cinemachine успешно захватила материализованного Игрока!");
                            }
                        }

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }


            // =========================================================================
            // 💀 [РАЗДЕЛ ЮНИТОВ] КЕЙС Б: ТЕХНИЧЕСКИЙ КУЛЛИНГ МОНСТРОВ (Ушли далеко)
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<UnitComponent, VisualizedTag>() // Ищем тех, кто в мире и имеет 3D-тело
                     .WithEntityAccess())
            {
                if (math.distance(playerPos, transform.ValueRO.Position) > 55f)
                {
                    var views = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
                    foreach (var view in views)
                    {
                        if (view.entity == entity)
                        {
                            Debug.Log($"[Visibility-Culling]: Сношу 3D-тело монстра {view.gameObject.name} по дистанции.");
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


using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

using ProjectTowerRpg.ECS.Components;
using GameObject = UnityEngine.GameObject;
using Resources = UnityEngine.Resources;
using Quaternion = UnityEngine.Quaternion;
using Object = UnityEngine.Object;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;
using Debug = UnityEngine.Debug;

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

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
            float3 playerPosition = playerTransform.Position;

            // =========================================================================
            // 🎒 [РАЗДЕЛ ПРЕДМЕТОВ] КЕЙС А: СПАВН ГРАФИКИ ПРЕДМЕТОВ
            // =========================================================================
            foreach (var (transform, item, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ItemComponent>>()
                     .WithNone<StoredTag>()     
                     .WithNone<VisualizedTag>() 
                     .WithEntityAccess())
            {
                float3 itemPosition = transform.ValueRO.Position;
                if (math.distance(playerPosition, itemPosition) <= 50f)
                {
                    // Исправлено: itemPrefab вместо unitPrefab
                    var itemPrefab = Resources.Load<GameObject>("Items/default_item");
                    if (itemPrefab != null)
                    {
                        var itemInstance = Object.Instantiate(itemPrefab, itemPosition, Quaternion.identity);
                        
                        // Исправлено: чистый нейминг без суффикса Str
                        string generatedUid = $"i_{(int)itemPosition.x}_{(int)itemPosition.z}";
                        string itemId = item.ValueRO.ItemId.ToString();
                        itemInstance.name = $"{itemId}_{generatedUid}";

                        // Исправлено: itemView вместо абстрактного view
                        var itemView = itemInstance.GetComponent<ItemView>();
                        if (itemView != null)
                        {
                            itemView.uid = generatedUid;
                            itemView.itemId = itemId;
                            itemView.IsLinked = true;
                            itemView.Entity = entity;
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
                if (math.distance(playerPosition, transform.ValueRO.Position) > 55f)
                {
                    var itemViews = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
                    foreach (var itemView in itemViews)
                    {
                        if (itemView.Entity == entity)
                        {
                            Object.Destroy(itemView.gameObject);
                            break;
                        }
                    }
                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }

            // =========================================================================
            // 💀 [РАЗДЕЛ ЮНИТОВ] КЕЙС А: МАТЕРИАЛИЗАЦИЯ МОНСТРОВ / NPC / ИГРОКА
            // =========================================================================
            foreach (var (transform, unit, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<UnitComponent>>()
                     .WithNone<VisualizedTag>() 
                     .WithEntityAccess())
            {
                float3 unitPosition = transform.ValueRO.Position;
                if (math.distance(playerPosition, unitPosition) <= 50f)
                {
                    string unitId = unit.ValueRO.UnitId.ToString();
                    var unitPrefab = Resources.Load<GameObject>($"Units/{unitId}");
                    
                    if (unitPrefab == null)
                    {
                        unitPrefab = Resources.Load<GameObject>("Units/default_unit");
                    }

                    if (unitPrefab != null)
                    {
                        var unitInstance = Object.Instantiate(unitPrefab, unitPosition, Quaternion.identity);
                        
                        string currentUid = unit.ValueRO.Uid.ToString();
                        bool isPlayerEntity = EntityManager.HasComponent<PlayerTag>(entity);
                        unitInstance.name = $"{unitId}_{(isPlayerEntity ? "player" : currentUid)}";

                        // Исправлено: unitView вместо абстрактного view
                        var unitView = unitInstance.GetComponent<UnitView>();
                        if (unitView != null)
                        {
                            unitView.uid = currentUid;
                            unitView.unitId = unitId;
                            
                            unitView.LinkToEntity(entity);
                        }

                        var syncTransform = unitInstance.GetComponent<SyncTransformWithEntity>();
                        if (syncTransform != null)
                        {
                            syncTransform.Initialize(entity);
                        }

                        if (isPlayerEntity)
                        {
                            var orbitCam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                            if (orbitCam != null)
                            {
                                orbitCam.Follow = unitInstance.transform;
                                orbitCam.LookAt = unitInstance.transform;
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
                     .WithAll<UnitComponent, VisualizedTag>() 
                     .WithEntityAccess())
            {
                if (math.distance(playerPosition, transform.ValueRO.Position) > 55f)
                {
                    // Исправлено: unitViews вместо абстрактного views
                    var unitViews = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
                    foreach (var unitView in unitViews)
                    {
                        if (unitView.entity == entity)
                        {
                            Debug.Log($"[Visibility-Culling]: Сношу 3D-тело монстра {unitView.gameObject.name} по дистанции.");
                            Object.Destroy(unitView.gameObject);
                            break;
                        }
                    }
                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }
        }
    }
}


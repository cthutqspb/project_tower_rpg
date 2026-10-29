using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.Core.Presentation;
using ProjectTowerRpg.ECS.Components;
using System.Collections.Generic;

using GameObject = UnityEngine.GameObject;
using Resources = UnityEngine.Resources;
using Quaternion = UnityEngine.Quaternion;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class EntityVisibilitySystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityCommandBufferSystem _ecbSystem;

        // 🎯 Радиусы выносим в константы
        private const float MATERIALIZE_DISTANCE = 50f;
        private const float CULL_DISTANCE = 55f;
        private const float MATERIALIZE_DISTANCE_SQ = MATERIALIZE_DISTANCE * MATERIALIZE_DISTANCE;
        private const float CULL_DISTANCE_SQ = CULL_DISTANCE * CULL_DISTANCE;

        // 🎯 КЭШ ПРЕФАБОВ (чтобы не читать с диска каждый раз)
        private Dictionary<string, GameObject> _prefabCache = new();
        private GameObject _defaultItemPrefab;
        private GameObject _defaultUnitPrefab;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            
            // Предзагружаем дефолтные префабы один раз
            _defaultItemPrefab = Resources.Load<GameObject>("Items/default_item");
            _defaultUnitPrefab = Resources.Load<GameObject>("Units/default_unit");
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
                
                if (math.distancesq(playerPosition, itemPosition) <= MATERIALIZE_DISTANCE_SQ)
                {
                    if (_defaultItemPrefab != null)
                    {
                        var itemInstance = Object.Instantiate(_defaultItemPrefab, itemPosition, Quaternion.identity);
                        
                        string generatedUid = $"i_{(int)itemPosition.x}_{(int)itemPosition.z}";
                        string itemId = item.ValueRO.ItemId.ToString();
                        itemInstance.name = $"{itemId}_{generatedUid}";

                        var itemView = itemInstance.GetComponent<ItemView>();
                        if (itemView != null)
                        {
                            itemView.uid = generatedUid;
                            itemView.itemId = itemId;
                            itemView.LinkToEntity(entity);
                        }

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }

            // =========================================================================
            // 🎒 [РАЗДЕЛ ПРЕДМЕТОВ] КЕЙС Б: КУЛЛИНГ ДИСТАНЦИИ
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithNone<StoredTag>() 
                     .WithAll<VisualizedTag, ItemComponent>() 
                     .WithEntityAccess())
            {
                if (math.distancesq(playerPosition, transform.ValueRO.Position) > CULL_DISTANCE_SQ)
                {
                    var itemView = EntityViewRegistry.Get<ItemView>(entity);
                    if (itemView != null)
                    {
                        Object.Destroy(itemView.gameObject);
                        EntityViewRegistry.Unregister(entity);
                    }                                     
                    
                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }

            // =========================================================================
            // 💀 [РАЗДЕЛ ЮНИТОВ] КЕЙС А: МАТЕРИАЛИЗАЦИЯ
            // =========================================================================
            foreach (var (transform, unit, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<UnitComponent>>()
                     .WithNone<VisualizedTag>() 
                     .WithEntityAccess())
            {
                float3 unitPosition = transform.ValueRO.Position;
                
                if (math.distancesq(playerPosition, unitPosition) <= MATERIALIZE_DISTANCE_SQ)
                {
                    string unitId = unit.ValueRO.UnitId.ToString();
                    string prefabPath = $"Units/{unitId}";

                    // 🎯 Берем из кэша или грузим 1 раз
                    if (!_prefabCache.TryGetValue(prefabPath, out var unitPrefab))
                    {
                        unitPrefab = Resources.Load<GameObject>(prefabPath) ?? _defaultUnitPrefab;
                        _prefabCache[prefabPath] = unitPrefab;
                    }

                    if (unitPrefab != null)
                    {
                        var unitInstance = Object.Instantiate(unitPrefab, unitPosition, Quaternion.identity);
                        
                        string currentUid = unit.ValueRO.Uid.ToString();
                        bool isPlayerEntity = EntityManager.HasComponent<PlayerTag>(entity);
                        unitInstance.name = $"{unitId}_{(isPlayerEntity ? "player" : currentUid)}";

                        var unitView = unitInstance.GetComponent<UnitView>();
                        if (unitView == null)
                        {
                            Debug.LogError($"[Materialize] Префаб '{unitPrefab.name}' не имеет UnitView!");
                            Object.Destroy(unitInstance);
                            continue;
                        }

                        unitView.uid = currentUid;
                        unitView.unitId = unitId;
                        unitView.LinkToEntity(entity);

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
                            }
                        }

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }

            // =========================================================================
            // 💀 [РАЗДЕЛ ЮНИТОВ] КЕЙС Б: КУЛЛИНГ
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<UnitComponent, VisualizedTag>() 
                     .WithEntityAccess())
            {
                if (math.distancesq(playerPosition, transform.ValueRO.Position) > CULL_DISTANCE_SQ)
                {
                    var unitView = EntityViewRegistry.Get<UnitView>(entity);
                    
#if UNITY_EDITOR
                    Debug.Log($"[Visibility-Culling]: entity {entity.Index} unitView {unitView}");
#endif

                    if (unitView != null)
                    {
#if UNITY_EDITOR
                        Debug.Log($"[Visibility-Culling]: Сношу 3D-тело монстра {unitView.gameObject.name} по дистанции.");
#endif
                        Object.Destroy(unitView.gameObject);
                        EntityViewRegistry.Unregister(entity);
                    }

                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }

            // =========================================================================
            // 🚀 [РАЗДЕЛ СНАРЯДОВ] КЕЙС А: МАТЕРИАЛИЗАЦИЯ
            // =========================================================================
            foreach (var (transform, projectileMovement, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectileMovement>>()
                     .WithAll<ProjectileTag>()
                     .WithNone<VisualizedTag>() 
                     .WithEntityAccess())
            {
                float3 projectilePosition = transform.ValueRO.Position;

                if (math.distancesq(playerPosition, projectilePosition) <= MATERIALIZE_DISTANCE_SQ)
                {
                    string prefabPath = projectileMovement.ValueRO.PrefabPath.ToString();

                    if (!_prefabCache.TryGetValue(prefabPath, out var projectilePrefab))
                    {
                        projectilePrefab = Resources.Load<GameObject>(prefabPath);
                        _prefabCache[prefabPath] = projectilePrefab;
                    }
                    
                    if (projectilePrefab != null)
                    {
                        var projectileInstance = Object.Instantiate(projectilePrefab, projectilePosition, Quaternion.identity);
                        projectileInstance.name = $"{projectileMovement.ValueRO.AbilityId}_projectile_{entity.Index}";

                        var syncTransform = projectileInstance.GetComponent<SyncTransformWithEntity>();
                        if (syncTransform == null)
                        {
                            syncTransform = projectileInstance.AddComponent<SyncTransformWithEntity>();
                        }

                        syncTransform.Type = SyncTransformWithEntity.ViewType.Projectile; 
                        syncTransform.Initialize(entity);
                        ProjectileViewRegistry.Register(entity, syncTransform);

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }

            // =========================================================================
            // 🚀 [РАЗДЕЛ СНАРЯДОВ] КЕЙС Б: КУЛЛИНГ
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<ProjectileTag, VisualizedTag>() 
                     .WithEntityAccess())
            {
                if (math.distancesq(playerPosition, transform.ValueRO.Position) > CULL_DISTANCE_SQ)
                {                    
                    var projectileView = ProjectileViewRegistry.Get(entity);
                    if (projectileView != null)
                        Object.Destroy(projectileView.gameObject);

                    ProjectileViewRegistry.Unregister(entity);
                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }
        }
    }
}

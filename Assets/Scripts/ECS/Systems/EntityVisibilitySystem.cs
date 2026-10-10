using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.Core.Presentation;
using ProjectTowerRpg.ECS.Components;

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
                            itemView.LinkToEntity(entity);
                        }

                        ecb.AddComponent<VisualizedTag>(entity);
                    }
                }
            }

            // =========================================================================
            // 🎒 [РАЗДЕЛ ПРЕДМЕТОВ] КЕЙС Б: СВЕРХЗВУКOВОЙ КУЛЛИНГ ДИСТАНЦИИ
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithNone<StoredTag>() 
                     .WithAll<VisualizedTag, ItemComponent>() 
                     .WithEntityAccess())
            {
                if (math.distance(playerPosition, transform.ValueRO.Position) > 55f)
                {
                    // 🦾 ИСТИННЫЙ ECS-КАНOН: За 0 наносекунд точечно достаем куб из телефонной книги по Entity!
                    // Никаких покадровых сканирований иерархии сцены и переборов сотен объектов!
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

                        var unitView = unitInstance.GetComponent<UnitView>();
                        if (unitView == null)
                        {
                            Debug.LogError($"[Materialize] Префаб '{unitPrefab.name}' не имеет UnitView! " +
                                           $"Юнит {unitId} (entity {entity.Index}) не будет зарегистрирован.");
                            Object.Destroy(unitInstance);   // откат — снести сломанный инстанс
                            continue;                        // не вешаем VisualizedTag
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
                    var unitView = EntityViewRegistry.Get<UnitView>(entity);
                    //if (unitView.entity != entity)
                    Debug.Log($"[Visibility-Culling]: entity {entity.Index} unitView {unitView}");

                    if (unitView != null)
                    {
                        Debug.Log($"[Visibility-Culling]: Сношу 3D-тело монстра {unitView.gameObject.name} по дистанции.");
                        Object.Destroy(unitView.gameObject);
                        EntityViewRegistry.Unregister(entity);
                    }

                    ecb.RemoveComponent<VisualizedTag>(entity);
                }
            }


            // =========================================================================
            // 🚀 [РАЗДЕЛ СНАРЯДОВ] КЕЙС А: СТEРИЛЬНАЯ МАТЕРИАЛИЗАЦИЯ (Без использования Core.Abilities)
            // =========================================================================
            foreach (var (transform, projectileMovement, entity) in 
                     SystemAPI.Query< RefRO< LocalTransform >, RefRO< ProjectileMovement > >()
                     .WithAll< ProjectileTag >()
                     .WithNone< VisualizedTag >() 
                     .WithEntityAccess())
            {
                float3 projectilePosition = transform.ValueRO.Position;

                if (math.distance(playerPosition, projectilePosition) <= 50f)
                {
                    // 🦾 ШЛЮЗ: Достаем путь к префабу НАПРЯМУЮ из ECS-компонента снаряда за 0 наносекунд!
                    string prefabPath = projectileMovement.ValueRO.PrefabPath.ToString();

                    var projectilePrefab = Resources.Load< GameObject >(prefabPath);
                    
                    if (projectilePrefab != null)
                    {
                        var projectileInstance = Object.Instantiate(projectilePrefab, projectilePosition, Quaternion.identity);
                        projectileInstance.name = $"{projectileMovement.ValueRO.AbilityId}_projectile_{entity.Index}";

                        // 🦾 СИ-ФИКС: Достаем компонент, жестко выставляем тип снаряда и только ПОТОМ инициализируем!
                        var syncTransform = projectileInstance.GetComponent<SyncTransformWithEntity>();
                        if (syncTransform == null)
                        {
                            syncTransform = projectileInstance.AddComponent<SyncTransformWithEntity>();
                        }

                        // Указываем тип роли!
                        syncTransform.Type = SyncTransformWithEntity.ViewType.Projectile; 
                        syncTransform.Initialize(entity);
                        ProjectileViewRegistry.Register(entity, syncTransform);

                        ecb.AddComponent< VisualizedTag >(entity);
                    }
                }
            }

            // =========================================================================
            // 🚀 [РАЗДЕЛ СНАРЯДОВ] КЕЙС Б: КУЛЛИНГ ДИСТАНЦИИ СНАРЯДОВ (Улетели далеко)
            // =========================================================================
            foreach (var (transform, entity) in 
                     SystemAPI.Query< RefRO< LocalTransform > >()
                     .WithAll< ProjectileTag, VisualizedTag >() 
                     .WithEntityAccess())
            {
                if (math.distance(playerPosition, transform.ValueRO.Position) > 55f)
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


using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using Unity.Transforms;
using ProjectTowerRpg.Core.Units;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UnitSpawnSystem : SystemBase
    {
        private readonly EquipSlot[] PAPERDOLL_SLOTS = new EquipSlot[]
        {
            EquipSlot.HEAD, EquipSlot.CHEST, EquipSlot.LEGS, EquipSlot.MAIN_HAND, EquipSlot.OFF_HAND
        };

        protected override void OnUpdate()
        {
            var em = EntityManager;
            
            // ================================================================
            // ОБРАБОТКА МАРКЕРОВ СПАВНА
            // ================================================================
            var markerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<UnitSpawnMarkerComponent>());
            
            if (markerQuery.IsEmpty)
            {
                Debug.Log("✅ [UnitSpawnSystem]: Фабрика успешно завершила работу. Все сущности созданы.");
                this.Enabled = false;
                return;
            }

            var markers = markerQuery.ToEntityArray(Allocator.Temp);

            foreach (var markerEntity in markers)
            {
                var markerData = em.GetComponentData<UnitSpawnMarkerComponent>(markerEntity);
                string uId = markerData.UnitId.ToString().ToLower().Trim();
                float3 spawnPos = markerData.SpawnPosition;

                var dbCfg = UnitsDatabase.GetUnit(uId);
                if (dbCfg == null)
                {
                    Debug.LogError($"🚨 ФАБРИКА: Юнит [{uId}] не найден в JSON-базе! Пропускаю.");
                    em.DestroyEntity(markerEntity);
                    continue;
                }

                // Вытаскиваем параметры из JSON-базы данных
                string nameKey = dbCfg.identity.name_key;
                float baseSpeed = dbCfg.parameters.base_speed;
                float hitboxRadius = dbCfg.parameters.hitbox_radius;

                float levelModifier = math.pow(dbCfg.progression.health_growth, markerData.Level - 1);
                int maxHealth = Mathf.FloorToInt(dbCfg.parameters.base_health * levelModifier);

                string rankStr = markerData.Rank.ToString().ToLower();
                float rankMultiplier = 1.0f;
                if (rankStr == "rare") rankMultiplier = 1.5f;
                else if (rankStr == "elite") rankMultiplier = 3.0f;
                else if (rankStr == "boss") rankMultiplier = 5.0f;

                maxHealth = Mathf.FloorToInt(maxHealth * rankMultiplier);

                string generatedUid = $"c_{Mathf.FloorToInt(spawnPos.x + 0.5f)}_{Mathf.FloorToInt(spawnPos.z + 0.5f)}";

                // 🏗️ 1. РОЖДАЕМ КРИСТАЛЬНО ЧИСТУЮ ECS-СУЩНОСТЬ ДУШИ С НУЛЯ (Без участия префабов!)
                Entity unitEntity = em.CreateEntity();

                em.AddComponentData(unitEntity, new UnitComponent
                {
                    Uid = markerData.IsPlayer ? "player" : generatedUid,
                    UnitId = uId,
                    NameKey = markerData.IsPlayer ? "player" : nameKey,
                    Level = markerData.Level,
                    Position = spawnPos
                });

                em.AddComponentData(unitEntity, new LocalTransform
                {
                    Position = spawnPos,
                    Rotation = quaternion.identity,
                    Scale = 1.0f
                });

                em.AddComponentData(unitEntity, new MovementComponent
                {
                    speed = baseSpeed,
                    direction = float3.zero,
                    isGrounded = true,
                    jumpRequested = false
                });

                em.AddComponentData(unitEntity, new CombatStateComponent
                {
                    IsDead = false, IsInCombat = false, CurrentHp = maxHealth, MaxHp = maxHealth,
                    BaseSpeed = baseSpeed, CurrentSpeed = baseSpeed, HitboxRadius = hitboxRadius
                });

                bool isPlayer = markerData.IsPlayer;

                em.AddComponentData(unitEntity, new HealthComponent
                {
                    Current = maxHealth,
                    Max = maxHealth
                });

                // 2. Читаем тип ресурса из твоего конфига dbCfg и переводим в ECS Enum
                ResourceType rType = ResourceType.None;
                float currentResource = 0f;
                float maxResource = 0f;

                if (dbCfg != null && dbCfg.resource != null)
                {
                    maxResource = dbCfg.resource.max;
                    currentResource = dbCfg.resource.current;

                    string resTypeStr = dbCfg.resource.type.ToString().ToLower().Trim();

                    if (resTypeStr == "mana") rType = ResourceType.Mana;
                    else if (resTypeStr == "energy") rType = ResourceType.Energy;
                    else if (resTypeStr == "rage") rType = ResourceType.Rage;
                    
                    // 🌟 ТЕСТ-ХАК: Если это игрок и данные пришли из конфига, режем ману пополам
                    if (markerData.IsPlayer)
                    {
                        currentResource = maxResource * 0.5f;
                    }
                }
                else
                {
                    rType = markerData.IsPlayer ? ResourceType.Mana : ResourceType.None;
                    maxResource = markerData.IsPlayer ? 100f : 0f;
                    
                    // 🌟 ТЕСТ-ХАК: Если это игрок и сработал дефолтный фоллбек, тоже заполняем наполовину
                    currentResource = markerData.IsPlayer ? (maxResource * 0.5f) : 0f;
                }

                // Вшиваем ресурсный компонент в сущность
                em.AddComponentData(unitEntity, new ResourceComponent
                {
                    Type = rType,
                    Current = currentResource,
                    Max = maxResource
                });

                em.AddComponentData(unitEntity, new AiComponent
                {
                    IsFromFactory = !isPlayer, 
                    StartPoint = spawnPos, 
                    PatrolRadius = isPlayer ? 0f : 4.0f,
                    CurrentTarget = spawnPos, 
                    NextActionTime = 0f, 
                    HasTarget = false, 
                    IsPatrolling = false
                });

                if (isPlayer)
                {
                    em.AddComponent<PlayerTag>(unitEntity);
                     // ================================================================
                    // 🔥 БУФЕР ВЗАИМОДЕЙСТВИЙ (ТОЛЬКО ДЛЯ ИГРОКА)
                    // ================================================================
                    em.AddBuffer<InteractionEntry>(unitEntity);

                    var playerAi = em.GetComponentData<AiComponent>(unitEntity);
                    playerAi.IsFromFactory = false;
                    playerAi.PatrolRadius = 0f;
                    em.SetComponentData(unitEntity, playerAi);
                }
                else
                {
                    em.AddComponent<MonsterTag>(unitEntity);
                }

                // 🏗️ 2. СТРОИМ БАЗОВЫЙ ИНВЕНТАРЬ (72 слота для всех под будущее расширение)
                var inventoryEntity = em.CreateEntity();
                em.AddComponentData(inventoryEntity, new ContainerConfigComponent { Owner = unitEntity, Columns = 6, Rows = 12 });
                em.AddComponent<InventoryTag>(inventoryEntity);
                var slotsBuffer = em.AddBuffer<SlotData>(inventoryEntity);
                for (int i = 0; i < 72; i++)
                {
                    slotsBuffer.Add(new SlotData { SlotIndex = i, DataId = "", DataType = "", Amount = 0, EquipSlot = EquipSlot.NONE, ContainerType = ContainerType.INVENTORY });
                }

                // 🏗️ 3. СТРОИМ КУКЛУ ШМОТА
                var paperdollEntity = em.CreateEntity();
                em.AddComponentData(paperdollEntity, new ContainerConfigComponent { Owner = unitEntity, Columns = PAPERDOLL_SLOTS.Length, Rows = 1 });
                em.AddComponent<PaperdollTag>(paperdollEntity);
                var paperdollBuffer = em.AddBuffer<SlotData>(paperdollEntity);
                for (int j = 0; j < PAPERDOLL_SLOTS.Length; j++)
                {
                    paperdollBuffer.Add(new SlotData { SlotIndex = j, DataId = "", DataType = "", Amount = 0, EquipSlot = PAPERDOLL_SLOTS[j], ContainerType = ContainerType.PAPERDOLL });
                }

                // 🎒 НАКЫДЫВАНИЕ ТЕСТОВОГО ШМОТА В ИНВЕНТАРЬ ИГРОКА (ПЕРЕНЕСЕНО ПОД ОБЪЯВЛЕНИЕ ПЕРЕМЕННОЙ)
                if (isPlayer)
                {
                    var testItems = new (string id, int amount)[]
                    {   
                        ("crystal_sword", 1),
                        ("iron_sword", 1),
                        ("crystal_sword", 1),
                        ("leather_helmet", 1),
                        ("clown_hat", 1),
                        ("lesser_mana_potion", 5),
                        ("chest_common", 1)
                    };

                    var playerSlotsBuffer = em.GetBuffer<SlotData>(inventoryEntity);

                    for (int idx = 0; idx < testItems.Length; idx++)
                    {
                        playerSlotsBuffer[idx] = new SlotData
                        {
                            SlotIndex = idx,
                            DataId = testItems[idx].id,
                            DataType = "item",
                            Amount = testItems[idx].amount,
                            EquipSlot = EquipSlot.NONE,
                            ContainerType = ContainerType.INVENTORY
                        };
                    }
                    
                    Debug.Log("🎒 [ФАБРИКА]: Тестовый шмот успешно упакован в инвентарь игрока!");
                }

                // =========================================================================
                // 🏗️ 4. ДИНАМИЧЕСКИЙ СПАВН 3D-ВИЗУАЛА ИЗ ПАПКИ RESOURCES/UNITS/
                // =========================================================================
                var visualPrefab = Resources.Load<GameObject>($"Units/{uId}");
                if (visualPrefab != null)
                {
                    var spawnedModel = Object.Instantiate(visualPrefab, spawnPos, Quaternion.identity);
                    spawnedModel.name = $"{uId}_{(isPlayer ? "player" : generatedUid)}";

                    // Инициализируем скрипт синхронизации (Оживляем LateUpdate и анимации Эми!)
                    var syncScript = spawnedModel.GetComponent<SyncTransformWithEntity>();
                    if (syncScript != null)
                    {
                        syncScript.Initialize(unitEntity);
                    }

                    // Настраиваем паспорт UnitView
                    var viewScript = spawnedModel.GetComponent<UnitView>();
                    if (viewScript != null)
                    {
                        viewScript.uid = isPlayer ? "player" : generatedUid;
                        viewScript.unitId = uId;
                        viewScript.entity = unitEntity;
                        viewScript.IsLinked = true;
                    }

                    // 🎥 Авто-привязка камеры Cinemachine для управляемого Игрока
                    if (isPlayer)
                    {
                        var orbitCam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                        if (orbitCam != null)
                        {
                            orbitCam.Follow = spawnedModel.transform;
                            orbitCam.LookAt = spawnedModel.transform;
                            Debug.Log("🎥 [ФАБРИКА]: Камера Cinemachine успешно захватила цель!");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"🚨 ФАБРИКА: Не удалось найти 3D-префаб по пути Assets/Prefabs/Resources/Units/{uId}.prefab!");
                }

                // Уничтожаем сущность кубика-метки
                em.DestroyEntity(markerEntity);

            }

            markers.Dispose();
        }
    }
}


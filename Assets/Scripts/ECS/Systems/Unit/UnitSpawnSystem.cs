using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using Unity.Transforms;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Data; // Наш зрячий GameDB

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
                
                // 🦾 ВОЗВРАЩАЕМ РОДНУЮ СТАБИЛЬНУЮ СТРОКОВУЮ ЛOГИКУ:
                string uId = markerData.UnitId.ToString().ToLower().Trim();
                
                // Ищем строго по строке, как это работало изначально!
                var dbCfg = UnitsDatabase.GetUnit(uId);
                if (dbCfg == null)
                {
                    Debug.LogError($"🚨 ФАБРИКА: Юнит [{uId}] не найден в JSON-базе! Пропускаю маркер.");
                    em.DestroyEntity(markerEntity);
                    continue;
                }

                float3 spawnPos = markerData.SpawnPosition;
                bool isPlayer = markerData.IsPlayer;

                // Вытаскиваем параметры из нашей статической JSON ДНК
                string nameKey = dbCfg.identity.name_key;
                float baseSpeed = dbCfg.parameters.base_speed;
                float hitboxRadius = dbCfg.parameters.hitbox_radius;

                // Теперь генерация Uid видит spawnPos идеально!
                string generatedUid = $"c_{Mathf.FloorToInt(spawnPos.x + 0.5f)}_{Mathf.FloorToInt(spawnPos.z + 0.5f)}";

                // 🏗️ 1. РОЖДАЕМ КРИСТАЛЬНО ЧИСТУЮ ECS-СУЩНОСТЬ ДУШИ С НУЛЯ
                Entity unitEntity = em.CreateEntity();

                em.AddComponentData(unitEntity, new UnitComponent
                {
                    Uid = isPlayer ? "player" : generatedUid,
                    UnitId = markerData.UnitId, // Передаем FixedString
                    NameKey = isPlayer ? "player" : nameKey,
                    Level = markerData.Level,
                    Position = spawnPos
                });

                em.AddComponentData(unitEntity, LocalTransform.FromPosition(spawnPos));

                em.AddComponentData(unitEntity, new MovementComponent
                {   
                    HitboxRadius = hitboxRadius,
                    CurrentSpeed = baseSpeed,
                    BaseSpeed = baseSpeed,
                    Direction = float3.zero,
                    IsGrounded = true,
                    JumpRequested = false
                });

                // Избавились от дублирования ХП и скорости! Оставили только чистый боевой стейт
                em.AddComponentData(unitEntity, new CombatStateComponent
                {
                    IsDead = false, 
                    IsInCombat = false
                });

                em.AddComponentData(unitEntity, new UnitCombatStatsComponent{});

                // Записываем РЕАЛЬНОЕ расчетное ХП из конфига в ОЗУ чанка при рождении!
                em.AddComponentData(unitEntity, new HealthComponent
                {
                    Current = dbCfg.parameters.base_health,
                    Max = dbCfg.parameters.base_health
                });

                // 🧬 🦾 ЗАПЕКАЕМ РПГ-МOНОЛИТ ХАРАКТЕРИСТИК (Симметрично твоему JSON):
                // Копируем базовую "голую тушу" из конфига. Сюда будет смотреть SaveManager!
                em.AddComponentData(unitEntity, new UnitBaseAttributesComponent
                {
                    strength = dbCfg.attributes.strength,
                    agility = dbCfg.attributes.agility,
                    intellect = dbCfg.attributes.intellect,
                    wisdom = dbCfg.attributes.wisdom,
                    stamina = dbCfg.attributes.stamina
                });

                // Инициализируем пустой runtime-черновик для UnitStatsSystem
                em.AddComponentData(unitEntity, new UnitCurrentAttributesComponent());

                // 🧬 🦾 ЗАПЕКАЕМ СГРУППИРОВАННЫЙ ПАСПОРТ ПРОГРЕССИИ:
                // Рассчитываем награду опыта на основе твоего базового уровня из маркера
                int calculatedExperienceReward = markerData.Level * 25; // Твой каноничный ММО-фарм
                
                // =========================================================================
                // 🦾 ОПРЕДЕЛЕНИЕ РАНГА СУЩЕСТВА (Приоритет: Маркер на сцене ➔ Конфиг из JSON)
                // =========================================================================
                string selectedRankStr = markerData.Rank.ToString().ToLower().Trim();

                // Если на маркере в редакторе ничего не настроили — берём дефолтный ранг из JSON
                if (string.IsNullOrEmpty(selectedRankStr))
                {
                    selectedRankStr = dbCfg.identity.default_rank.ToLower().Trim();
                }

                // Мапим строковое значение на наш чистый unmanaged энум UnitRank
                UnitRank Rank = UnitRank.Normal;

                if (selectedRankStr == "rare") Rank = UnitRank.Rare;
                else if (selectedRankStr == "elite") Rank = UnitRank.Elite;
                else if (selectedRankStr == "boss") Rank = UnitRank.Boss;

                em.AddComponentData(unitEntity, new UnitProgressionComponent
                {
                    Level = markerData.Level,
                    ExperienceCurrent = 0,
                    ExperienceRequired = markerData.Level * 100, // Условный левел-кап опыта
                    ExperienceReward = calculatedExperienceReward,

                    Rank = Rank,
                    GrowthHealth = dbCfg.progression.growth_health,
                    GrowthDamage = dbCfg.progression.growth_damage
                });

                // ================================================================
                // 🔮 ИНИЦИАЛИЗАЦИЯ И РАСЧЕТ РЕСУРСА (Мана, Энергия, Ярость)
                // ================================================================
                ResourceType rType = ResourceType.None;
                float currentResource = 0f;
                float maxResource = 0f;

                if (dbCfg.resource != null)
                {
                    maxResource = dbCfg.resource.max;
                    currentResource = dbCfg.resource.current;

                    // Избавились от текстовой лапши! Newtonsoft.Json нагло парсит энум напрямую,
                    // но если в конфиге осталась строка — сравниваем чистые байты без аллокаций.
                    string resTypeStr = dbCfg.resource.type.ToLower();
                    if (resTypeStr == "mana") rType = ResourceType.Mana;
                    else if (resTypeStr == "energy") rType = ResourceType.Energy;
                    else if (resTypeStr == "rage") rType = ResourceType.Rage;
                    
                    // 🌟 ТЕСТ-ХАК: Если это игрок — по ММО-канону режем ману наполовину при старте кадра
                    if (isPlayer)
                    {
                        currentResource = maxResource * 0.5f;
                    }
                }
                else
                {
                    rType = isPlayer ? ResourceType.Mana : ResourceType.None;
                    maxResource = isPlayer ? 100f : 0f;
                    currentResource = isPlayer ? (maxResource * 0.5f) : 0f;
                }

                em.AddComponentData(unitEntity, new ResourceComponent
                {
                    Type = rType,
                    Current = currentResource,
                    Max = maxResource
                });

                // 🧠 ЗРЯЧАЯ НАСТРОЙКА ИИ: Никаких лишних перезаписей памяти!
                // Забиваем параметры структуры сразу в зависимости от того, игрок это или моб!
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
                    em.AddComponent<ControlledByPlayerTag>(unitEntity);
                    
                    // ================================================================
                    // 🔥 БУФЕР ВЗАИМОДЕЙСТВИЙ (ТОЛЬКО ДЛЯ ИГРОКА)
                    // ================================================================
                    em.AddBuffer<InteractionEntry>(unitEntity);
                }
                else
                {
                    em.AddComponent<MonsterTag>(unitEntity);
                }

                // ================================================================
                // 🏗️ 2. СТРОИМ БАЗОВЫЙ ИНВЕНТАРЬ (72 слота)
                // ================================================================
                var inventoryEntity = em.CreateEntity();
                em.AddComponentData(inventoryEntity, new ContainerConfigComponent { Owner = unitEntity, Columns = 6, Rows = 12 });
                em.AddComponent<InventoryTag>(inventoryEntity);
                
                var slotsBuffer = em.AddBuffer<ItemSlot>(inventoryEntity);
                for (int i = 0; i < 72; i++)
                {
                    slotsBuffer.Add(new ItemSlot 
                    { 
                        SlotIndex = i, 
                        DataId = "", 
                        DataType = "", 
                        Amount = 0, 
                        EquipSlot = EquipSlot.NONE, 
                        ContainerType = ContainerType.INVENTORY 
                    });
                }

                // ================================================================
                // 🏗️ 3. СТРОИМ КУКЛУ ШМОТА (Paperdoll)
                // ================================================================
                var paperdollEntity = em.CreateEntity();
                em.AddComponentData(paperdollEntity, new ContainerConfigComponent { Owner = unitEntity, Columns = PAPERDOLL_SLOTS.Length, Rows = 1 });
                em.AddComponent<PaperdollTag>(paperdollEntity);
                
                var paperdollBuffer = em.AddBuffer<ItemSlot>(paperdollEntity);
                for (int j = 0; j < PAPERDOLL_SLOTS.Length; j++)
                {
                    paperdollBuffer.Add(new ItemSlot 
                    { 
                        SlotIndex = j, 
                        DataId = "", 
                        DataType = "", 
                        Amount = 0, 
                        EquipSlot = PAPERDOLL_SLOTS[j], 
                        ContainerType = ContainerType.PAPERDOLL 
                    });
                }

                // 🦾 ЗАПЕКАЕМ СВЯЗИ С БУФЕРАМИ: Теперь юнит намертво знает адреса своих карманов!
                em.AddComponentData(unitEntity, new BuffersLinkComponent
                {
                    Inventory = inventoryEntity,
                    Paperdoll = paperdollEntity
                });

                // ================================================================
                // 🎒 НАПОЛНЕНИЕ ТЕСТОВОГО ШМОТА В ИНВЕНТАРЬ ИГРОКА
                // ================================================================
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

                    // 🦾 ШАГ 1: Спавним "души" предметов в ОЗУ
                    NativeArray<Entity> spawnedItemEntities = new NativeArray<Entity>(testItems.Length, Allocator.Temp);

                    for (int idx = 0; idx < testItems.Length; idx++)
                    {
                        Entity itemEntity = em.CreateEntity();
                        
                        em.AddComponentData(itemEntity, new ItemComponent
                        {
                            Uid = testItems[idx].id.GetHashCode() + idx, 
                            ItemId = testItems[idx].id,
                            Amount = testItems[idx].amount,
                            IsLooted = true 
                        });

                        spawnedItemEntities[idx] = itemEntity;
                    }

                    // 🦾 ШАГ 2: Стабилизируем ОЗУ и скачиваем легитимный буфер инвентаря игрока
                    var playerSlotsBuffer = em.GetBuffer<ItemSlot>(inventoryEntity);

                    // 🦾 ШАГ 3: Шёлково заполняем ячейки рюкзака ссылками на вечные Entity предмета
                    for (int idx = 0; idx < testItems.Length; idx++)
                    {
                        playerSlotsBuffer[idx] = new ItemSlot
                        {
                            SlotIndex = idx,
                            DataId = testItems[idx].id,
                            DataType = "item",
                            Amount = testItems[idx].amount,
                            ItemEntity = spawnedItemEntities[idx], 
                            EquipSlot = EquipSlot.NONE,
                            ContainerType = ContainerType.INVENTORY
                        };
                    }

                    spawnedItemEntities.Dispose();
                    Debug.Log("🎒 [ФАБРИКА]: Стартовые шмотки получили свои вечные ECS-сущности и упакованы в рюкзак!");

                    // ================================================================
                    // 🦾 НАПОЛНЕНИЕ ЭКШЕН-БАРА (Плоский сквозной массив хоткеев 0..23)
                    // ================================================================
                    var barBuffer = em.AddBuffer<ActionBarSlot>(unitEntity);

                    for (int k = 0; k < 24; k++)
                    {
                        // Вычисляем локальный Си-хоткей на базе сквозного индекса (0..23)
                        int localIndex = k % 12;
                        string defaultKey = localIndex switch
                        {
                            9 => "0",
                            10 => "-",
                            11 => "=",
                            _ => (localIndex + 1).ToString()
                        };

                        if (k == 0)
                        {
                            barBuffer.Add(new ActionBarSlot { SlotIndex = k, AbilityId = "melee_attack", SlotType = "spell", KeyBinding = defaultKey });
                        }
                        else if (k == 1)
                        {
                            barBuffer.Add(new ActionBarSlot { SlotIndex = k, AbilityId = "frostbolt", SlotType = "spell", KeyBinding = defaultKey });
                        }
                        else
                        {
                            barBuffer.Add(new ActionBarSlot { SlotIndex = k, AbilityId = "", SlotType = "", KeyBinding = defaultKey });
                        }
                    }

                    Debug.Log("🔮 [ФАБРИКА]: Сквозной массив хоткеев (0..23) успешно вшит в буфер игрока!");
                }   
                
                // Уничтожаем кубик-маркер запроса спавна прямо через EntityManager
                em.DestroyEntity(markerEntity);
            }

            markers.Dispose();
        }
    }
}


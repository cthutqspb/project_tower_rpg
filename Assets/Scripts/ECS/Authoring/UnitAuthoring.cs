using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units; // Твой реальный домен, где лежат все базы!

namespace ProjectTowerRpg.ECS.Authoring
{
    public class UnitAuthoring : MonoBehaviour
    {
        [Header("Defold go.property Аналоги")]
        public string uid; 
        public string unitId = "skeleton_warrior";
        public int unitLevel = 1;
        public string unitRank = "common"; 
        public string lootTableId = "empty";
        
        [Header("Flags")]
        public bool isPlayer = false;
    }

    public class UnitBaker : Baker<UnitAuthoring>
    {
        private readonly EquipSlot[] PAPERDOLL_SLOTS = new EquipSlot[]
        {
            EquipSlot.HEAD, 
            EquipSlot.CHEST,
            EquipSlot.LEGS,
            EquipSlot.MAIN_HAND,
            EquipSlot.OFF_HAND
        };

        public override void Bake(UnitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // ИСПРАВЛЕНО: Честно берем позицию из GameObject-авторинга в Unity
            float3 spawnPosition = authoring.transform.position;

            // 🎯 1. ЧЕСТНОЕ ОКРУГЛЕНИЕ UID ПО КООРДИНАТАМ (Твой оригинальный Lua-алгоритм)
            string stringUid = authoring.uid;
            if (string.IsNullOrEmpty(stringUid))
            {
                stringUid = $"c_{Mathf.FloorToInt(spawnPosition.x + 0.5f)}_{Mathf.FloorToInt(spawnPosition.z + 0.5f)}";
            }

            float baseSpeed = 5.0f; // Честные 5 м/с для игрока по дефолту
            float hitboxRadius = 0.5f; // Честные 0.5 метра
            int calculatedMaxHealth = 100;

            if (!authoring.isPlayer)
            {
                var dbCfg = UnitsDatabase.GetUnit(authoring.unitId);
                if (dbCfg != null)
                {
                    // ИСПРАВЛЕНО: Никакой лапши с делениями! Берем чистые метры прямо из файла!
                    baseSpeed = dbCfg.parameters.base_speed;
                    hitboxRadius = dbCfg.parameters.hitbox_radius;

                    // Расчёт ХП по уровню и рангу оставляем без изменений
                    float levelModifier = Mathf.Pow(dbCfg.progression.health_growth, authoring.unitLevel - 1);
                    calculatedMaxHealth = Mathf.FloorToInt(dbCfg.parameters.base_health * levelModifier);

                    float rankMultiplier = 1.0f;
                    if (authoring.unitRank == "rare") rankMultiplier = 1.5f;
                    else if (authoring.unitRank == "elite") rankMultiplier = 3.0f;
                    else if (authoring.unitRank == "boss") rankMultiplier = 5.0f;

                    calculatedMaxHealth = Mathf.FloorToInt(calculatedMaxHealth * rankMultiplier);
                }
            }
            else
            {
                // Игрок со сцены получает эталонный 3D-базис
                baseSpeed = 5.0f; 
                hitboxRadius = 0.5f;
                calculatedMaxHealth = 100;
            }

            // ================================================================
            // 🧱 3. ЗАПЕКАНИЕ КОМПОНЕНТОВ ДУШИ В СИ-ПАМЯТЬ
            // ================================================================
            
            // Всаживаем универсальный паспорт
            AddComponent(entity, new UnitComponent
            {
                Uid = stringUid,
                UnitId = authoring.unitId,
                Level = authoring.unitLevel,
                Position = spawnPosition
            });

            // Накатываем твой MovementComponent с физикой прыжков
            AddComponent(entity, new MovementComponent
            {
                speed = baseSpeed,
                direction = float3.zero,
                isGrounded = true,
                jumpRequested = false
            });

            // Накатываем мутабельный боевой паспорт здоровья
            AddComponent(entity, new CombatStateComponent
            {
                IsDead = false,
                IsInCombat = false,
                CurrentHp = calculatedMaxHealth,
                MaxHp = calculatedMaxHealth,
                BaseSpeed = baseSpeed,
                CurrentSpeed = baseSpeed,
                HitboxRadius = hitboxRadius
            });

            // Накатываем память пассивного ИИ-автомата патруля
            AddComponent(entity, new AiComponent
            {   
                IsFromFactory = !authoring.isPlayer, 
                StartPoint = spawnPosition,
                PatrolRadius = 70f,
                CurrentTarget = spawnPosition,
                NextActionTime = 0f,
                HasTarget = false,
                IsPatrolling = false
            });

            // ================================================================
            // 🏷️ МАРКЕРЫ ФРАКЦИЙ И РАЗДЕЛЕНИЯ РАЗУМА ВСЕЛЕННОЙ
            // ================================================================
            if (authoring.isPlayer)
            {
                AddComponent<PlayerTag>(entity);
            }
            else
            {
                // ИСПРАВЛЕНО НАМЕРТВО: Никаких дубликатов! Накатываем чистый маркер монстра
                AddComponent<MonsterTag>(entity);
            }


            // ================================================================
            // 🎒 4. ИНВЕНТАРЬ (Запекается на лету Си-буфером для ВСЕХ юнитов!)
            // ================================================================
            int inventorySlotsCount = authoring.isPlayer ? 49 : 24;

            var inventoryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(inventoryEntity, new ContainerConfigComponent
            {
                Owner = entity,
                Columns = authoring.isPlayer ? 6 : 4,
                Rows = authoring.isPlayer ? 12 : 6
            });

            // Кристально чистый вызов буфера для рюкзака
            var slotsBuffer = AddBuffer<SlotData>(inventoryEntity);
            
            // Массив стартового лута игрока из твоего UnitSpawnSystem
            var testItems = new (string id, int amount)[]
            {
                ("iron_sword", 1),
                ("crystal_sword", 1),
                ("leather_helmet", 1),
                ("clown_hat", 1),
                ("lesser_mana_potion", 5)
            };

            for (int idx = 0; idx < inventorySlotsCount; idx++)
            {
                // По умолчанию ячейка пустая
                string itemId = "";
                string dataType = "";
                int itemAmount = 0;

                // Если это Игрок, первые 5 слотов забиваем тестовым шмотом
                if (authoring.isPlayer && idx < testItems.Length)
                {
                    itemId = testItems[idx].id;
                    dataType = "item";
                    itemAmount = testItems[idx].amount;
                }

                slotsBuffer.Add(new SlotData
                {
                    SlotIndex = idx,
                    DataId = itemId,
                    DataType = dataType,
                    Amount = itemAmount,
                    EquipSlot = EquipSlot.NONE,
                    ContainerType = ContainerType.INVENTORY
                });
            }

            // ================================================================
            // 👕 5. КУКЛА ШМОТА (Запекается на лету Си-буфером для ВСЕХ юнитов!)
            // ================================================================
            var paperdollEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(paperdollEntity, new ContainerConfigComponent
            {
                Owner = entity,
                Columns = PAPERDOLL_SLOTS.Length,
                Rows = 1
            });

            var paperdollSlotsBuffer = AddBuffer<SlotData>(paperdollEntity);
            for (int j = 0; j < PAPERDOLL_SLOTS.Length; j++)
            {
                paperdollSlotsBuffer.Add(new SlotData
                {
                    SlotIndex = j,
                    DataId = "",
                    DataType = "",
                    Amount = 0,
                    EquipSlot = PAPERDOLL_SLOTS[j],
                    ContainerType = ContainerType.PAPERDOLL
                });
            }
        }
    }
}


using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units; // Твоя база данных JSON

namespace ProjectTowerRpg.ECS.Authoring
{
    public class UnitAuthoring : MonoBehaviour
    {
        [Header("Идентификатор типа юнита в JSON-базе")]
        public string unitId = "skeleton_warrior";
        
        [Header("Стартовые параметры (если нет в JSON)")]
        public int unitLevel = 1;
        public string unitRank = "common"; 
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
            
            // Базовые дефолты
            float baseSpeed = 5.0f; 
            float hitboxRadius = 0.5f; 
            int calculatedMaxHealth = 100;

            // Стучимся в JSON-базу данных за параметрами
            var dbCfg = UnitsDatabase.GetUnit(authoring.unitId);
            if (dbCfg != null)
            {
                baseSpeed = dbCfg.parameters.base_speed;
                hitboxRadius = dbCfg.parameters.hitbox_radius;

                float levelModifier = Mathf.Pow(dbCfg.progression.growth_health, authoring.unitLevel - 1);
                calculatedMaxHealth = Mathf.FloorToInt(dbCfg.parameters.base_health * levelModifier);

                float rankMultiplier = 1.0f;
                string rankStr = authoring.unitRank.ToLower();
                if (rankStr == "rare") rankMultiplier = 1.5f;
                else if (rankStr == "elite") rankMultiplier = 3.0f;
                else if (rankStr == "boss") rankMultiplier = 5.0f;

                calculatedMaxHealth = Mathf.FloorToInt(calculatedMaxHealth * rankMultiplier);
            }

            // ================================================================
            // 🧱 ЗАПЕКАНИЕ БАЗОВЫХ КОМПОНЕНТОВ ДУШИ
            // ================================================================
            
            // Паспорт сущности (UID сгенерирует фабрика при спавне на сцене!)
            AddComponent(entity, new UnitComponent
            {
                Uid = "", 
                UnitId = authoring.unitId,
                Level = authoring.unitLevel,
                Position = float3.zero 
            });

            // Компонент движения и физики
            AddComponent(entity, new MovementComponent
            {   
                HitboxRadius = hitboxRadius,
                BaseSpeed = baseSpeed,
                CurrentSpeed = baseSpeed,
                Direction = float3.zero,
                IsGrounded = true,
                JumpRequested = false
            });

            // Боевой паспорт здоровья
            AddComponent(entity, new CombatStateComponent
            {
                IsDead = false,
                IsInCombat = false, 
            });

            // Память ИИ (по умолчанию включен, фабрика выключит его, если это Игрок)
            AddComponent(entity, new AiComponent
            {   
                IsFromFactory = true, 
                StartPoint = float3.zero,
                PatrolRadius = 4.0f,
                CurrentTarget = float3.zero,
                NextActionTime = 0f,
                HasTarget = false,
                IsPatrolling = false
            });

            // ================================================================
            // 🎒 ИНВЕНТАРЬ (Запекаем базовую сетку под размер будущего игрока)
            // ================================================================
            // Делаем базовые 72 слота (6х12) для основы. В будущем под BG3-систему
            // этот буфер сможет динамически расширяться прямо в рантайме.
            int inventorySlotsCount = 72;

            var inventoryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(inventoryEntity, new ContainerConfigComponent
            {
                Owner = entity,
                Columns = 6,
                Rows = 12
            });

            var slotsBuffer = AddBuffer<ItemSlot>(inventoryEntity);
            for (int idx = 0; idx < inventorySlotsCount; idx++)
            {
                slotsBuffer.Add(new ItemSlot
                {
                    SlotIndex = idx,
                    DataId = "",
                    DataType = "",
                    Amount = 0,
                    EquipSlot = EquipSlot.NONE,
                    ContainerType = ContainerType.INVENTORY
                });
            }

            var paperdollEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(paperdollEntity, new ContainerConfigComponent
            {
                Owner = entity,
                Columns = PAPERDOLL_SLOTS.Length,
                Rows = 1
            });

            var paperdollSlotsBuffer = AddBuffer<ItemSlot>(paperdollEntity);
            for (int j = 0; j < PAPERDOLL_SLOTS.Length; j++)
            {
                paperdollSlotsBuffer.Add(new ItemSlot
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


using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class UnitSpawnSystem : SystemBase
    {
        private bool _spawned = false;

        protected override void OnUpdate()
        {
            if (_spawned) return;

            // ================================================================
            // 1. СОЗДАЁМ ЮНИТА
            // ================================================================
            var unitEntity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(unitEntity, new UnitComponent
            {
                Uid = "player",
                UnitId = "player_mage",
                Level = 1,
                Position = new float3(0, 0, 0)
            });

            EntityManager.AddComponent<PlayerTag>(unitEntity);

            // ================================================================
            // 2. СОЗДАЁМ ИНВЕНТАРЬ (отдельная Entity)
            // ================================================================
            var inventoryEntity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(inventoryEntity, new StaticGridComponent
            {
                SlotCount = 24,
                Owner = unitEntity
            });
            EntityManager.AddComponent<InventoryTag>(inventoryEntity);

            // Инициализируем пустой буфер с индексами ячеек
            var slots = EntityManager.AddBuffer<SlotData>(inventoryEntity);
            for (int i = 0; i < 24; i++)
            {
                slots.Add(new SlotData
                {
                    SlotIndex = i, // ← ВАЖНО: сохраняем индекс ячейки
                    DataId = "",
                    DataType = "",
                    Amount = 0,
                    EquipSlot = "",
                    ContainerType = "inventory" // ← ВАЖНО: задаем тип контейнера
                });
            }

            // Заполняем тестовыми предметами
            var testItems = new (string id, int amount)[]
            {
                ("iron_sword", 1),
                ("crystal_sword", 1),
                ("leather_helmet", 1),
                ("clown_hat", 1),
                ("lesser_mana_potion", 5)
            };

            for (int i = 0; i < testItems.Length && i < slots.Length; i++)
            {
                slots[i] = new SlotData
                {
                    SlotIndex = i, // ← ВАЖНО: не теряем индекс при перезаписи структуры
                    DataId = testItems[i].id,
                    DataType = "item",
                    Amount = testItems[i].amount,
                    EquipSlot = "",
                    ContainerType = "inventory" // ← ВАЖНО: тип контейнера
                };
            }

            // ================================================================
            // 3. РЕГИСТРИРУЕМ В ENTITYREGISTRY (инвентарь, НЕ юнит!)
            // ================================================================
            Debug.Log($"[UnitSpawnSystem] Регистрирую инвентарь: {inventoryEntity}");
            EntityRegistry.Register("unit_inventory", inventoryEntity);

            Debug.Log($"[UnitSpawnSystem] Юнит создан. Инвентарь: {slots.Length} slots");

            _spawned = true;
        }
    }
}


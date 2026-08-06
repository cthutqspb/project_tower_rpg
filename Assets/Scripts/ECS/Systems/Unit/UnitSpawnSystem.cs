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

        private const int INVENTORY_COLUMNS = 6;
        private const int INVENTORY_ROWS = 12;
        private const int INVENTORY_SLOTS = INVENTORY_COLUMNS * INVENTORY_ROWS;

        private readonly EquipSlot[] PAPERDOLL_SLOTS = new EquipSlot[]
        {
            EquipSlot.HEAD, 
            EquipSlot.CHEST,
            EquipSlot.LEGS,
            EquipSlot.MAIN_HAND,
            EquipSlot.OFF_HAND
        };

        protected override void OnUpdate()
        {
            if (_spawned) return;

            // ================================================================
            // 1. ЮНИТ
            // ================================================================
            var unitEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(unitEntity, new UnitComponent
            {
                Uid = "player",
                UnitId = "player_mage",
                Level = 1,
                Position = new float3(0, 0, 0)
            });

            // ================================================================
            // 2. ИНВЕНТАРЬ
            // ================================================================
            var inventoryEntity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(inventoryEntity, new ContainerConfigComponent
            {
                Columns = INVENTORY_COLUMNS,
                Rows = INVENTORY_ROWS,
                Owner = unitEntity
            });

            var slots = EntityManager.AddBuffer<SlotData>(inventoryEntity);
            for (int i = 0; i < INVENTORY_SLOTS; i++)
            {
                slots.Add(new SlotData
                {
                    SlotIndex = i,
                    DataId = "",
                    DataType = "",
                    Amount = 0,
                    EquipSlot = EquipSlot.NONE,             // ИСПРАВЛЕНО: enum вместо ""
                    ContainerType = ContainerType.INVENTORY // ИСПРАВЛЕНО: enum вместо "inventory"
                });
            }

            // Тестовые предметы
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
                    SlotIndex = i,
                    DataId = testItems[i].id,
                    DataType = "item",
                    Amount = testItems[i].amount,
                    EquipSlot = EquipSlot.NONE,             // ИСПРАВЛЕНО: enum вместо ""
                    ContainerType = ContainerType.INVENTORY // ИСПРАВЛЕНО: enum вместо "inventory"
                };
            }

            EntityRegistry.Register("unit_inventory", inventoryEntity);

            // ================================================================
            // 3. КУКЛА (ПЕРЕВЕДЕНА НА SlotData)
            // ================================================================
            var paperdollEntity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(paperdollEntity, new ContainerConfigComponent
            {
                Owner = unitEntity,
                Columns = PAPERDOLL_SLOTS.Length,
                Rows = 1
            });

            // ИСПРАВЛЕНО: Добавляем буфер универсального SlotData вместо старого PaperdollSlot!
            var paperdollSlots = EntityManager.AddBuffer<SlotData>(paperdollEntity);
            for (int i = 0; i < PAPERDOLL_SLOTS.Length; i++)
            {
                paperdollSlots.Add(new SlotData
                {
                    SlotIndex = i, // Индекс ячейки куклы (0, 1, 2...)
                    DataId = "",
                    DataType = "",
                    Amount = 0,
                    EquipSlot = PAPERDOLL_SLOTS[i],          // Назначение слота (Head, Chest...)
                    ContainerType = ContainerType.PAPERDOLL  // Указываем, что этот буфер — кукла
                });
            }

            EntityRegistry.Register("unit_paperdoll", paperdollEntity);

            Debug.Log($"[UnitSpawnSystem] Юнит создан. Инвентарь: {INVENTORY_SLOTS} slots. Кукла: {PAPERDOLL_SLOTS.Length} универсальных slots.");

            _spawned = true;
        }
    }
}


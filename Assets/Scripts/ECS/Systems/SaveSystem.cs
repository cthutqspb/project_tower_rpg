using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using System.Collections.Generic;
using System.IO;
using Application = UnityEngine.Application;
using JsonUtility = UnityEngine.JsonUtility;
using Resources = UnityEngine.Resources;
using Object = UnityEngine.Object;
using GameObject = UnityEngine.GameObject;
using Quaternion = UnityEngine.Quaternion;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class SaveSystem : SystemBase
    {
        private string SavePath => Application.persistentDataPath + "/save.json";

        private bool _isF9Latched = false;
        private bool _isF5Latched = false;

        protected override void OnUpdate()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.isPressed && !_isF5Latched)
            {
                _isF5Latched = true;
                Save();
            }
            else
            {
                _isF5Latched = false;
            }

            if (keyboard.f9Key.isPressed && !_isF9Latched)
            {
                _isF9Latched = true;
                Load();
            }
            else
            {
                _isF9Latched = false;
            }
        }

        // ================================================================
        // SAVE
        // ================================================================
        public void Save()
        {
            var saveData = new SaveData();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // ================================================================
            // 1. СОБИРАЕМ ВСЕ ПРЕДМЕТЫ СРАЗУ — И В МИРЕ, И В КОНТЕЙНЕРАХ.
            //    Строим словарь uid -> ItemSaveData, чтобы слоты юнитов
            //    могли ссылаться на Uid предмета.
            // ================================================================
            var itemQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            var allItemEntities = itemQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in allItemEntities)
            {
                var itemComp = em.GetComponentData<ItemComponent>(entity);

                var itemData = new ItemSaveData
                {
                    Uid = itemComp.Uid.ToString(),
                    ItemId = itemComp.ItemId.ToString(),
                    Amount = itemComp.Amount,
                    LootTableId = itemComp.LootTableId.ToString(),
                    RespawnTime = itemComp.RespawnTime,
                    IsLootGenerated = em.HasComponent<LootGeneratedTag>(entity),
                };

                // Позиция — только если предмет НЕ в контейнере
                if (!em.HasComponent<StoredTag>(entity) && em.HasComponent<LocalTransform>(entity))
                {
                    itemData.Position = em.GetComponentData<LocalTransform>(entity).Position;
                }

                // Если контейнер — сохраняем содержимое bagEntity
                if (em.HasComponent<BuffersLinkComponent>(entity))
                {
                    var links = em.GetComponentData<BuffersLinkComponent>(entity);

                    if (links.Inventory != Entity.Null
                        && em.Exists(links.Inventory)
                        && em.HasBuffer<ItemSlot>(links.Inventory))
                    {
                        var slots = em.GetBuffer<ItemSlot>(links.Inventory);
                        foreach (var slot in slots)
                        {
                            string childUid = "";
                            if (slot.ItemEntity != Entity.Null
                                && em.Exists(slot.ItemEntity)
                                && em.HasComponent<ItemComponent>(slot.ItemEntity))
                            {
                                childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();
                            }

                            itemData.ContainerSlots.Add(new ItemSlotSaveData
                            {
                                Uid = childUid,
                                ItemId = slot.DataId.ToString(),
                                Amount = slot.Amount,
                                EquipSlot = slot.EquipSlot,
                                ContainerType = slot.ContainerType,
                            });
                        }
                    }
                }

                saveData.Items.Add(itemData);
            }
            allItemEntities.Dispose();

            // ================================================================
            // 2. СОХРАНЯЕМ ЮНИТОВ
            // ================================================================
            var allUnitsQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<HealthComponent>(),
                ComponentType.ReadOnly<ResourceComponent>(),
                ComponentType.ReadOnly<BuffersLinkComponent>()
            );
            var unitEntities = allUnitsQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in unitEntities)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                var transform = em.GetComponentData<LocalTransform>(entity);
                var health = em.GetComponentData<HealthComponent>(entity);
                var resources = em.GetComponentData<ResourceComponent>(entity);
                var buffersLink = em.GetComponentData<BuffersLinkComponent>(entity);

                var unitData = new UnitSaveData
                {
                    Uid = unit.Uid.ToString(),
                    UnitId = unit.UnitId.ToString(),
                    Level = unit.Level,
                    Position = transform.Position,
                    Health = health.Current,
                    MaxHealth = health.Max,
                    Mana = resources.Current,
                    MaxMana = resources.Max,
                    IsPlayer = em.HasComponent<PlayerTag>(entity),
                    IsLeader = em.HasComponent<LeaderTag>(entity),
                    IsMonster = em.HasComponent<MonsterTag>(entity),
                    IsNpc = em.HasComponent<NpcTag>(entity),
                };

                // Инвентарь
                if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                {
                    var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                    foreach (var slot in slots)
                    {
                        string childUid = "";
                        if (slot.ItemEntity != Entity.Null
                            && em.Exists(slot.ItemEntity)
                            && em.HasComponent<ItemComponent>(slot.ItemEntity))
                        {
                            childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();
                        }

                        unitData.InventorySlots.Add(new ItemSlotSaveData
                        {
                            Uid = childUid,
                            ItemId = slot.DataId.ToString(),
                            Amount = slot.Amount,
                            EquipSlot = slot.EquipSlot,
                            ContainerType = slot.ContainerType,
                        });
                    }
                }

                // Кукла
                if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                {
                    var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                    foreach (var slot in slots)
                    {
                        string childUid = "";
                        if (slot.ItemEntity != Entity.Null
                            && em.Exists(slot.ItemEntity)
                            && em.HasComponent<ItemComponent>(slot.ItemEntity))
                        {
                            childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();
                        }

                        unitData.PaperdollSlots.Add(new ItemSlotSaveData
                        {
                            Uid = childUid,
                            ItemId = slot.DataId.ToString(),
                            Amount = slot.Amount,
                            EquipSlot = slot.EquipSlot,
                            ContainerType = slot.ContainerType,
                        });
                    }
                }

                // Экшенбар
                if (em.HasBuffer<ActionBarSlot>(entity))
                {
                    var barSlots = em.GetBuffer<ActionBarSlot>(entity);
                    foreach (var barSlot in barSlots)
                    {
                        unitData.ActionBarSlots.Add(new ActionBarSaveData
                        {
                            SlotIndex = barSlot.SlotIndex,
                            AbilityId = barSlot.AbilityId.ToString(),
                            SlotType = barSlot.SlotType.ToString(),
                        });
                    }
                }

                saveData.Units.Add(unitData);
            }
            unitEntities.Dispose();

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"💾 Save: юнитов {saveData.Units.Count}, предметов {saveData.Items.Count}");
        }

        // ================================================================
        // LOAD
        // ================================================================
        public void Load()
        {
            if (!File.Exists(SavePath)) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            string json = File.ReadAllText(SavePath);
            var saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null) return;

            // 1. Удаляем старые маркеры юнитов
            em.DestroyEntity(em.CreateEntityQuery(typeof(UnitSpawnMarkerComponent)));

            // 2. Собираем uid -> Entity для всех существующих ItemEntity
            var allItemsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            var existingItems = allItemsQuery.ToEntityArray(Allocator.Temp);
            var uidToItemEntity = new Dictionary<string, Entity>();

            foreach (var entity in existingItems)
            {
                var itemComp = em.GetComponentData<ItemComponent>(entity);
                uidToItemEntity[itemComp.Uid.ToString()] = entity;
            }
            existingItems.Dispose();

            // 3. Собираем uid -> Entity для юнитов
            var playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            var existingPlayers = playerQuery.ToEntityArray(Allocator.Temp);
            var uidToPlayer = new Dictionary<string, Entity>();
            foreach (var entity in existingPlayers)
                uidToPlayer[em.GetComponentData<UnitComponent>(entity).Uid.ToString()] = entity;
            existingPlayers.Dispose();

            var monsterQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<MonsterTag>()
            );
            var existingMonsters = monsterQuery.ToEntityArray(Allocator.Temp);
            var uidToMonster = new Dictionary<string, Entity>();
            foreach (var entity in existingMonsters)
                uidToMonster[em.GetComponentData<UnitComponent>(entity).Uid.ToString()] = entity;
            existingMonsters.Dispose();

            // 4. Включаем систему спавна юнитов
            var spawnSystem = World.GetExistingSystemManaged<UnitSpawnSystem>();
            if (spawnSystem != null) spawnSystem.Enabled = true;

            // ================================================================
            // 5. ВОССТАНАВЛИВАЕМ ПРЕДМЕТЫ
            //    Если Uid найден — заливаем. Если нет — создаём.
            // ================================================================
            foreach (var itemData in saveData.Items)
            {
                Entity itemEntity;

                if (uidToItemEntity.TryGetValue(itemData.Uid, out itemEntity))
                {
                    // === СУЩЕСТВУЮЩИЙ ПРЕДМЕТ — ОБНОВЛЯЕМ ===
                    var itemComp = em.GetComponentData<ItemComponent>(itemEntity);
                    itemComp.ItemId = itemData.ItemId;
                    itemComp.Amount = itemData.Amount;
                    itemComp.LootTableId = itemData.LootTableId;
                    itemComp.RespawnTime = itemData.RespawnTime;
                    em.SetComponentData(itemEntity, itemComp);

                    // Позиция — если предмет не в контейнере
                    if (!em.HasComponent<StoredTag>(itemEntity) && em.HasComponent<LocalTransform>(itemEntity))
                    {
                        var t = em.GetComponentData<LocalTransform>(itemEntity);
                        t.Position = itemData.Position;
                        em.SetComponentData(itemEntity, t);
                    }

                    // LootGeneratedTag
                    if (itemData.IsLootGenerated && !em.HasComponent<LootGeneratedTag>(itemEntity))
                        em.AddComponent<LootGeneratedTag>(itemEntity);
                    else if (!itemData.IsLootGenerated && em.HasComponent<LootGeneratedTag>(itemEntity))
                        em.RemoveComponent<LootGeneratedTag>(itemEntity);

                    // bagEntity — заливаем слоты
                    if (em.HasComponent<BuffersLinkComponent>(itemEntity))
                    {
                        var links = em.GetComponentData<BuffersLinkComponent>(itemEntity);
                        if (links.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(links.Inventory))
                        {
                            var slots = em.GetBuffer<ItemSlot>(links.Inventory);
                            for (int i = 0; i < slots.Length && i < itemData.ContainerSlots.Count; i++)
                            {
                                var s = itemData.ContainerSlots[i];

                                Entity childEntity = Entity.Null;
                                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var child))
                                    childEntity = child;

                                slots[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = s.ItemId,
                                    DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                    Amount = s.Amount,
                                    ItemEntity = childEntity,
                                    EquipSlot = s.EquipSlot,
                                    ContainerType = s.ContainerType,
                                };
                            }
                        }
                    }
                }
                else
                {
                    // === НОВЫЙ ПРЕДМЕТ — СОЗДАЁМ ===
                    itemEntity = em.CreateEntity();

                    em.AddComponentData(itemEntity, LocalTransform.FromPosition(itemData.Position));
                    em.AddComponentData(itemEntity, new ItemComponent
                    {
                        Uid = int.Parse(itemData.Uid),
                        ItemId = itemData.ItemId,
                        Amount = itemData.Amount,
                        LootTableId = itemData.LootTableId,
                        RespawnTime = itemData.RespawnTime,
                    });

                    if (itemData.IsLootGenerated)
                        em.AddComponent<LootGeneratedTag>(itemEntity);

                    // Контейнер — если есть сохранённые слоты
                    if (itemData.ContainerSlots.Count > 0)
                    {
                        int totalSlots = itemData.ContainerSlots.Count;

                        Entity bagEntity = em.CreateEntity();
                        em.AddComponentData(bagEntity, new ContainerConfigComponent
                        {
                            Owner = itemEntity,
                            Columns = totalSlots,
                            Rows = 1,
                        });
                        em.AddComponent<InventoryTag>(bagEntity);

                        var slotsBuffer = em.AddBuffer<ItemSlot>(bagEntity);
                        for (int i = 0; i < totalSlots; i++)
                        {
                            var s = itemData.ContainerSlots[i];

                            Entity childEntity = Entity.Null;
                            if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var child))
                                childEntity = child;

                            slotsBuffer.Add(new ItemSlot
                            {
                                SlotIndex = i,
                                DataId = s.ItemId,
                                DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                Amount = s.Amount,
                                ItemEntity = childEntity,
                                EquipSlot = s.EquipSlot,
                                ContainerType = s.ContainerType,
                            });
                        }

                        em.AddComponentData(itemEntity, new BuffersLinkComponent
                        {
                            Inventory = bagEntity,
                            Paperdoll = Entity.Null,
                        });
                        em.AddComponent<ContainerTag>(itemEntity);
                    }

                    // Визуал
                    var prefab = Resources.Load<GameObject>("Items/default_item");
                    if (prefab != null)
                    {
                        var go = Object.Instantiate(prefab, itemData.Position, Quaternion.identity);
                        var view = go.GetComponent<ItemView>();
                        if (view != null)
                        {
                            view.uid = itemData.Uid;
                            view.itemId = itemData.ItemId;
                            view.entity = itemEntity;
                            view.IsLinked = true;
                        }
                    }

                    uidToItemEntity[itemData.Uid] = itemEntity;
                }
            }

            // ================================================================
            // 6. ВОССТАНАВЛИВАЕМ ЮНИТОВ
            // ================================================================
            foreach (var unitData in saveData.Units)
            {
                if (unitData.IsPlayer)
                {
                    if (uidToPlayer.TryGetValue(unitData.Uid, out Entity entity))
                    {
                        var unit = em.GetComponentData<UnitComponent>(entity);
                        unit.Level = unitData.Level;
                        em.SetComponentData(entity, unit);

                        var transform = em.GetComponentData<LocalTransform>(entity);
                        transform.Position = unitData.Position;
                        em.SetComponentData(entity, transform);

                        var health = em.GetComponentData<HealthComponent>(entity);
                        health.Current = (int)unitData.Health;
                        health.Max = (int)unitData.MaxHealth;
                        em.SetComponentData(entity, health);

                        var resources = em.GetComponentData<ResourceComponent>(entity);
                        resources.Current = unitData.Mana;
                        resources.Max = unitData.MaxMana;
                        em.SetComponentData(entity, resources);

                        var buffersLink = em.GetComponentData<BuffersLinkComponent>(entity);

                        // Инвентарь — с привязкой к ItemEntity по Uid
                        if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                            for (int i = 0; i < slots.Length && i < unitData.InventorySlots.Count; i++)
                            {
                                var s = unitData.InventorySlots[i];
                                Entity itemEnt = Entity.Null;
                                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var ie))
                                    itemEnt = ie;

                                slots[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = s.ItemId,
                                    DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                    Amount = s.Amount,
                                    ItemEntity = itemEnt,
                                    EquipSlot = s.EquipSlot,
                                    ContainerType = s.ContainerType,
                                };
                            }
                        }

                        // Кукла
                        if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                            for (int i = 0; i < slots.Length && i < unitData.PaperdollSlots.Count; i++)
                            {
                                var s = unitData.PaperdollSlots[i];
                                Entity itemEnt = Entity.Null;
                                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var ie))
                                    itemEnt = ie;

                                slots[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = s.ItemId,
                                    DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                    Amount = s.Amount,
                                    ItemEntity = itemEnt,
                                    EquipSlot = s.EquipSlot,
                                    ContainerType = s.ContainerType,
                                };
                            }
                        }

                        // Экшенбар
                        if (em.HasBuffer<ActionBarSlot>(entity) && unitData.ActionBarSlots.Count > 0)
                        {
                            var barSlots = em.GetBuffer<ActionBarSlot>(entity);
                            for (int k = 0; k < barSlots.Length && k < unitData.ActionBarSlots.Count; k++)
                            {
                                var targetBar = barSlots[k];
                                targetBar.AbilityId = unitData.ActionBarSlots[k].AbilityId;
                                targetBar.SlotType = unitData.ActionBarSlots[k].SlotType;
                                barSlots[k] = targetBar;
                            }
                        }

                        if (unitData.IsLeader)
                            em.AddComponent<LeaderTag>(entity);
                        else if (em.HasComponent<LeaderTag>(entity))
                            em.RemoveComponent<LeaderTag>(entity);
                    }
                }
                else
                {
                    // МОНСТР
                    if (uidToMonster.TryGetValue(unitData.Uid, out Entity entity))
                    {
                        var health = em.GetComponentData<HealthComponent>(entity);
                        health.Current = (int)unitData.Health;
                        health.Max = (int)unitData.MaxHealth;
                        em.SetComponentData(entity, health);

                        var transform = em.GetComponentData<LocalTransform>(entity);
                        transform.Position = unitData.Position;
                        em.SetComponentData(entity, transform);

                        var unit = em.GetComponentData<UnitComponent>(entity);
                        unit.Level = unitData.Level;
                        em.SetComponentData(entity, unit);

                        var resources = em.GetComponentData<ResourceComponent>(entity);
                        resources.Current = unitData.Mana;
                        resources.Max = unitData.MaxMana;
                        em.SetComponentData(entity, resources);

                        // Инвентарь
                        var buffersLink = em.GetComponentData<BuffersLinkComponent>(entity);
                        if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                            for (int i = 0; i < slots.Length && i < unitData.InventorySlots.Count; i++)
                            {
                                var s = unitData.InventorySlots[i];
                                Entity itemEnt = Entity.Null;
                                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var ie))
                                    itemEnt = ie;

                                slots[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = s.ItemId,
                                    DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                    Amount = s.Amount,
                                    ItemEntity = itemEnt,
                                    EquipSlot = s.EquipSlot,
                                    ContainerType = s.ContainerType,
                                };
                            }
                        }

                        // Кукла
                        if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                            for (int i = 0; i < slots.Length && i < unitData.PaperdollSlots.Count; i++)
                            {
                                var s = unitData.PaperdollSlots[i];
                                Entity itemEnt = Entity.Null;
                                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var ie))
                                    itemEnt = ie;

                                slots[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = s.ItemId,
                                    DataType = string.IsNullOrEmpty(s.ItemId) ? "" : "item",
                                    Amount = s.Amount,
                                    ItemEntity = itemEnt,
                                    EquipSlot = s.EquipSlot,
                                    ContainerType = s.ContainerType,
                                };
                            }
                        }
                    }
                    else
                    {
                        // Монстра нет — создаём маркер
                        Entity spawnMarker = em.CreateEntity();
                        em.AddComponentData(spawnMarker, new UnitSpawnMarkerComponent
                        {
                            PrefabEntity = Entity.Null,
                            UnitId = unitData.UnitId,
                            SpawnPosition = unitData.Position,
                            IsPlayer = false,
                            Level = unitData.Level,
                            Rank = "common",
                        });
                    }
                }
            }

            Debug.Log($"📂 Load: юнитов {saveData.Units.Count}, предметов {saveData.Items.Count}");
        }
    }

    // ================================================================
    // СТРУКТУРЫ ДАННЫХ ДЛЯ СЕРИАЛИЗАЦИИ
    // ================================================================
    [System.Serializable]
    public class SaveData
    {
        public List<UnitSaveData> Units = new();
        public List<ItemSaveData> Items = new();
    }

    [System.Serializable]
    public class UnitSaveData
    {
        public string Uid;
        public string UnitId;
        public int Level;
        public float3 Position;
        public float Health;
        public float MaxHealth;
        public float Mana;
        public float MaxMana;
        public bool IsPlayer;
        public bool IsLeader;
        public bool IsMonster;
        public bool IsNpc;

        public List<ItemSlotSaveData> InventorySlots = new();
        public List<ItemSlotSaveData> PaperdollSlots = new();
        public List<ActionBarSaveData> ActionBarSlots = new();
    }

    [System.Serializable]
    public class ItemSlotSaveData
    {
        public string Uid;             // Uid предмета, который лежит в слоте
        public string ItemId;
        public int Amount;
        public EquipSlot EquipSlot;
        public ContainerType ContainerType;
    }

    [System.Serializable]
    public class ActionBarSaveData
    {
        public int SlotIndex;
        public string AbilityId;
        public string SlotType;
    }

    [System.Serializable]
    public class ItemSaveData
    {
        public string Uid;
        public string ItemId;
        public int Amount;
        public float3 Position;
        public string LootTableId;
        public int RespawnTime;
        public bool IsLootGenerated;

        public List<ItemSlotSaveData> ContainerSlots = new();
    }
}

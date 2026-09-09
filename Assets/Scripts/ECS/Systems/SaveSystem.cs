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

        public void Save()
        {
            var saveData = new SaveData();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

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

                // Сохраняем инвентарь
                if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                {
                    var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                    foreach (var slot in slots)
                    {
                        unitData.InventorySlots.Add(new ItemSlotSaveData 
                        { 
                            ItemId = slot.DataId.ToString(), 
                            Amount = slot.Amount, 
                            EquipSlot = slot.EquipSlot, 
                            ContainerType = slot.ContainerType 
                        });
                    }
                }

                // Сохраняем куклу
                if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                {
                    var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                    foreach (var slot in slots)
                    {
                        unitData.PaperdollSlots.Add(new ItemSlotSaveData 
                        { 
                            ItemId = slot.DataId.ToString(), 
                            Amount = slot.Amount, 
                            EquipSlot = slot.EquipSlot, 
                            ContainerType = slot.ContainerType 
                        });
                    }
                }

                // Сохраняем экшенбар
                if (em.HasBuffer<ActionBarSlot>(entity))
                {
                    var barSlots = em.GetBuffer<ActionBarSlot>(entity);
                    foreach (var barSlot in barSlots)
                    {
                        unitData.ActionBarSlots.Add(new ActionBarSaveData
                        {
                            SlotIndex = barSlot.SlotIndex,
                            AbilityId = barSlot.AbilityId.ToString(),
                            SlotType = barSlot.SlotType.ToString()
                        });
                    }
                }

                saveData.Units.Add(unitData);
            }

            // Сохраняем предметы в мире
            var itemQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ItemComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<StoredTag>()
            );
            var itemEntities = itemQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in itemEntities)
            {
                var itemComp = em.GetComponentData<ItemComponent>(entity);
                var transform = em.GetComponentData<LocalTransform>(entity);
                saveData.WorldItems.Add(new WorldItemSaveData 
                { 
                    ItemId = itemComp.ItemId.ToString(), 
                    Amount = itemComp.Amount, 
                    Position = transform.Position 
                });
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"💾 [SaveSystem]: Мир запечен! Существ: {saveData.Units.Count}, Предметов на полу: {saveData.WorldItems.Count}");
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            string json = File.ReadAllText(SavePath);
            var saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null) return;

            // 1. Удаляем старые маркеры
            var oldMarkers = em.CreateEntityQuery(typeof(UnitSpawnMarkerComponent));
            em.DestroyEntity(oldMarkers);

            // 2. Удаляем старые предметы в мире
            var oldItemsQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ItemComponent>(),
                ComponentType.Exclude<StoredTag>()
            );
            em.DestroyEntity(oldItemsQuery);

            // 3. Кэшируем существующих игроков
            var playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            var existingPlayers = playerQuery.ToEntityArray(Allocator.Temp);
            var uidToEntity = new Dictionary<string, Entity>();

            foreach (var entity in existingPlayers)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                uidToEntity[unit.Uid.ToString()] = entity;
            }

            // 4. Кэшируем существующих монстров
            var monsterQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<MonsterTag>()
            );
            var existingMonsters = monsterQuery.ToEntityArray(Allocator.Temp);
            var uidToMonster = new Dictionary<string, Entity>();

            foreach (var entity in existingMonsters)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                uidToMonster[unit.Uid.ToString()] = entity;
            }

            // 5. Включаем систему спавна
            var spawnSystem = World.GetExistingSystemManaged<UnitSpawnSystem>();
            if (spawnSystem != null)
            {
                spawnSystem.Enabled = true;
            }

            // 6. Восстанавливаем всех юнитов
            foreach (var unitData in saveData.Units)
            {
                if (unitData.IsPlayer)
                {
                    if (uidToEntity.TryGetValue(unitData.Uid, out Entity entity))
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

                        // Восстанавливаем инвентарь
                        if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                            for (int i = 0; i < slots.Length && i < unitData.InventorySlots.Count; i++)
                            {
                                var targetSlot = slots[i];
                                targetSlot.DataId = unitData.InventorySlots[i].ItemId;
                                targetSlot.Amount = unitData.InventorySlots[i].Amount;
                                slots[i] = targetSlot;
                            }
                        }

                        // Восстанавливаем куклу
                        if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                            for (int i = 0; i < slots.Length && i < unitData.PaperdollSlots.Count; i++)
                            {
                                var targetSlot = slots[i];
                                targetSlot.DataId = unitData.PaperdollSlots[i].ItemId;
                                targetSlot.Amount = unitData.PaperdollSlots[i].Amount;
                                slots[i] = targetSlot;
                            }
                        }

                        // Восстанавливаем экшенбар
                        if (em.HasBuffer<ActionBarSlot>(entity) && unitData.ActionBarSlots.Count > 0)
                        {
                            var barSlots = em.GetBuffer<ActionBarSlot>(entity);
                            for (int k = 0; k < barSlots.Length && k < unitData.ActionBarSlots.Count; k++)
                            {
                                var savedBar = unitData.ActionBarSlots[k];
                                var targetBar = barSlots[k];
                                targetBar.AbilityId = savedBar.AbilityId;
                                targetBar.SlotType = savedBar.SlotType;
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
                        // Обновляем существующего монстра
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

                        // Восстанавливаем инвентарь
                        var buffersLink = em.GetComponentData<BuffersLinkComponent>(entity);
                        if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                            for (int i = 0; i < slots.Length && i < unitData.InventorySlots.Count; i++)
                            {
                                slots[i] = new ItemSlot
                                {
                                    DataId = unitData.InventorySlots[i].ItemId,
                                    Amount = unitData.InventorySlots[i].Amount,
                                    EquipSlot = unitData.InventorySlots[i].EquipSlot,
                                    ContainerType = unitData.InventorySlots[i].ContainerType
                                };
                            }
                        }

                        // Восстанавливаем куклу
                        if (buffersLink.Paperdoll != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Paperdoll))
                        {
                            var slots = em.GetBuffer<ItemSlot>(buffersLink.Paperdoll);
                            for (int i = 0; i < slots.Length && i < unitData.PaperdollSlots.Count; i++)
                            {
                                slots[i] = new ItemSlot
                                {
                                    DataId = unitData.PaperdollSlots[i].ItemId,
                                    Amount = unitData.PaperdollSlots[i].Amount,
                                    EquipSlot = unitData.PaperdollSlots[i].EquipSlot,
                                    ContainerType = unitData.PaperdollSlots[i].ContainerType
                                };
                            }
                        }
                    }
                    else
                    {
                        // Монстра нет — создаём маркер спавна
                        Entity spawnMarker = em.CreateEntity();
                        em.AddComponentData(spawnMarker, new UnitSpawnMarkerComponent
                        {
                            PrefabEntity = Entity.Null,
                            UnitId = unitData.UnitId,
                            SpawnPosition = unitData.Position,
                            IsPlayer = false,
                            Level = unitData.Level,
                            Rank = "common"
                        });
                    }
                }
            }

            // ================================================================
            // 7. ВОССТАНАВЛИВАЕМ ПРЕДМЕТЫ В МИРЕ
            // ================================================================
            foreach (var itemData in saveData.WorldItems)
            {
                // 🦾 СОЗДАЁМ СУЩНОСТЬ ПРЕДМЕТА НАПРЯМУЮ
                Entity itemEntity = em.CreateEntity();
                
                // Генерируем Uid
                string generatedUidStr = $"i_{(int)itemData.Position.x}_{(int)itemData.Position.z}";
                int generatedUidHash = generatedUidStr.GetHashCode();
                
                // Компоненты
                em.AddComponentData(itemEntity, LocalTransform.FromPosition(itemData.Position));
                em.AddComponentData(itemEntity, new ItemComponent
                {
                    Uid = generatedUidHash,
                    ItemId = itemData.ItemId,
                    Amount = itemData.Amount,
                    IsLooted = false,
                    LootTableId = "",
                    RespawnTime = 0
                });
                
                // Спавн визуала
                var prefab = Resources.Load<GameObject>("Items/default_item");
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, itemData.Position, Quaternion.identity);
                    var view = go.GetComponent<ItemView>();
                    if (view != null)
                    {
                        view.uid = generatedUidStr;
                        view.itemId = itemData.ItemId;
                        view.Entity = itemEntity;
                        view.IsLinked = true;
                    }
                }
            }

            Debug.Log($"📂 [SaveSystem]: Загрузка завершена! Восстановлено {saveData.Units.Count} юнитов, {saveData.WorldItems.Count} предметов.");
        }
    }

    // ================================================================
    // СТРУКТУРЫ ДАННЫХ ДЛЯ СЕРИАЛИЗАЦИИ
    // ================================================================
    [System.Serializable]
    public class SaveData
    {
        public List<UnitSaveData> Units = new();
        public List<WorldItemSaveData> WorldItems = new();
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
    public class WorldItemSaveData
    {
        public string ItemId;
        public int Amount;
        public float3 Position;
    }
}

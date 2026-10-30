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
using ProjectTowerRpg.Core.Quest;

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

            if (keyboard.f5Key.isPressed && !_isF5Latched) { _isF5Latched = true; Save(); }
            else { _isF5Latched = false; }

            if (keyboard.f9Key.isPressed && !_isF9Latched) { _isF9Latched = true; Load(); }
            else { _isF9Latched = false; }
        }

        // ================================================================
        // SAVE
        // ================================================================
        public void Save()
        {
            var saveData = new SaveData();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 1. ПРЕДМЕТЫ
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

                if (!em.HasComponent<StoredTag>(entity) && em.HasComponent<LocalTransform>(entity))
                    itemData.Position = em.GetComponentData<LocalTransform>(entity).Position;

                if (em.HasComponent<BuffersLinkComponent>(entity))
                {
                    var links = em.GetComponentData<BuffersLinkComponent>(entity);
                    if (links.Inventory != Entity.Null && em.Exists(links.Inventory) && em.HasBuffer<ItemSlot>(links.Inventory))
                    {
                        var slots = em.GetBuffer<ItemSlot>(links.Inventory);
                        foreach (var slot in slots)
                        {
                            string childUid = "";
                            if (slot.ItemEntity != Entity.Null && em.Exists(slot.ItemEntity) && em.HasComponent<ItemComponent>(slot.ItemEntity))
                                childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();

                            itemData.ContainerSlots.Add(new ItemSlotSaveData
                            {
                                Uid = childUid, 
                                ItemId = slot.DataId.ToString(), 
                                Amount = slot.Amount,
                                EquipSlot = slot.EquipSlot, 
                                ContainerType = slot.ContainerType,
                                ContentType = (byte)slot.ContentType // 🎯
                            });
                        }
                    }
                }
                saveData.Items.Add(itemData);
            }
            allItemEntities.Dispose();

            // 2. ЮНИТЫ
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
                    NameKey = unit.NameKey.ToString(),
                    UniqueNameKey = unit.UniqueNameKey.ToString(),
                    Race = unit.Race.ToString(),
                    Type = unit.Type.ToString(),
                    DefaultRank = unit.DefaultRank.ToString(),
                    Level = unit.Level,
                    Position = transform.Position,
                    Health = health.Current,
                    MaxHealth = health.Max,
                    Mana = resources.Current,
                    MaxMana = resources.Max,
                    IsDead = em.HasComponent<IsDeadTag>(entity),
                    IsPlayer = em.HasComponent<PlayerTag>(entity),
                    IsLeader = em.HasComponent<LeaderTag>(entity),
                    IsMonster = em.HasComponent<MonsterTag>(entity),
                    IsNpc = em.HasComponent<NpcTag>(entity),
                };

                // Базовые статы
                if (em.HasComponent<UnitBaseAttributesComponent>(entity))
                {
                    var attrs = em.GetComponentData<UnitBaseAttributesComponent>(entity);
                    unitData.BaseAttributes = new BaseAttributesSaveData
                    {
                        Strength = attrs.strength, Agility = attrs.agility, Intellect = attrs.intellect,
                        Wisdom = attrs.wisdom, Stamina = attrs.stamina,
                    };
                }

                // Прогрессия
                if (em.HasComponent<UnitProgressionComponent>(entity))
                {
                    var prog = em.GetComponentData<UnitProgressionComponent>(entity);
                    unitData.Progression = new ProgressionSaveData
                    {
                        Level = prog.Level, ExperienceCurrent = prog.ExperienceCurrent,
                        ExperienceRequired = prog.ExperienceRequired, ExperienceReward = prog.ExperienceReward,
                        Rank = prog.Rank.ToString(), GrowthHealth = prog.GrowthHealth, GrowthDamage = prog.GrowthDamage,
                    };
                }

                // Инвентарь
                if (buffersLink.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(buffersLink.Inventory))
                {
                    var slots = em.GetBuffer<ItemSlot>(buffersLink.Inventory);
                    foreach (var slot in slots)
                    {
                        string childUid = "";
                        if (slot.ItemEntity != Entity.Null && em.Exists(slot.ItemEntity) && em.HasComponent<ItemComponent>(slot.ItemEntity))
                            childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();

                        unitData.InventorySlots.Add(new ItemSlotSaveData
                        {
                            Uid = childUid, 
                            ItemId = slot.DataId.ToString(), 
                            Amount = slot.Amount,
                            EquipSlot = slot.EquipSlot, 
                            ContainerType = slot.ContainerType,
                            ContentType = (byte)slot.ContentType // 🎯
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
                        if (slot.ItemEntity != Entity.Null && em.Exists(slot.ItemEntity) && em.HasComponent<ItemComponent>(slot.ItemEntity))
                            childUid = em.GetComponentData<ItemComponent>(slot.ItemEntity).Uid.ToString();

                        unitData.PaperdollSlots.Add(new ItemSlotSaveData
                        {
                            Uid = childUid, 
                            ItemId = slot.DataId.ToString(), 
                            Amount = slot.Amount,
                            EquipSlot = slot.EquipSlot, 
                            ContainerType = slot.ContainerType,
                            ContentType = (byte)slot.ContentType // 🎯
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
                            ContentType = (byte)barSlot.ContentType // 🎯
                        });
                    }
                }

                // Квесты
                if (em.HasBuffer<QuestComponent>(entity))
                {
                    var quests = em.GetBuffer<QuestComponent>(entity);
                    foreach (var q in quests)
                    {
                        var qSave = new QuestSaveData
                        {
                            QuestId = q.QuestId.ToString(), State = q.State, IsTracked = q.IsTracked, RewardClaimed = q.RewardClaimed,
                        };

                        if (em.HasBuffer<QuestObjectiveComponent>(entity))
                        {
                            var objectives = em.GetBuffer<QuestObjectiveComponent>(entity);
                            foreach (var obj in objectives)
                            {
                                if (obj.QuestId == q.QuestId)
                                {
                                    qSave.Objectives.Add(new ObjectiveSaveData
                                    {
                                        ObjectiveId = obj.ObjectiveId.ToString(),
                                        Current = obj.Current, Required = obj.Required, IsCompleted = obj.IsCompleted,
                                    });
                                }
                            }
                        }
                        unitData.Quests.Add(qSave);
                    }
                }

                // Ауры
                if (buffersLink.AuraFrame != Entity.Null && em.HasBuffer<AuraSlot>(buffersLink.AuraFrame))
                {
                    var auraSlots = em.GetBuffer<AuraSlot>(buffersLink.AuraFrame);
                    foreach (var aura in auraSlots)
                    {
                        if (aura.IsEmpty) continue;
                        unitData.AuraSlots.Add(new AuraSlotSaveData
                        {
                            AuraId = aura.AuraId.ToString(), TimeRemaining = aura.TimeRemaining,
                            Duration = aura.Duration, Stacks = aura.Stacks,
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

            em.DestroyEntity(em.CreateEntityQuery(typeof(UnitSpawnMarkerComponent)));

            // Словарь предметов
            var allItemsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            var existingItems = allItemsQuery.ToEntityArray(Allocator.Temp);
            var uidToItemEntity = new Dictionary<string, Entity>();
            foreach (var entity in existingItems)
            {
                var itemComp = em.GetComponentData<ItemComponent>(entity);
                uidToItemEntity[itemComp.Uid.ToString()] = entity;
            }
            existingItems.Dispose();

            // Словарь юнитов
            var allUnitsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<UnitComponent>());
            var existingUnits = allUnitsQuery.ToEntityArray(Allocator.Temp);
            var uidToUnit = new Dictionary<string, Entity>();
            foreach (var entity in existingUnits)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                uidToUnit[unit.Uid.ToString()] = entity;
            }
            existingUnits.Dispose();

            var spawnSystem = World.GetExistingSystemManaged<UnitSpawnSystem>();
            if (spawnSystem != null) spawnSystem.Enabled = true;

            // 5. ВОССТАНАВЛИВАЕМ ПРЕДМЕТЫ
            foreach (var itemData in saveData.Items)
            {
                Entity itemEntity;

                if (uidToItemEntity.TryGetValue(itemData.Uid, out itemEntity))
                {
                    var itemComp = em.GetComponentData<ItemComponent>(itemEntity);
                    itemComp.ItemId = itemData.ItemId;
                    itemComp.Amount = itemData.Amount;
                    itemComp.LootTableId = itemData.LootTableId;
                    itemComp.RespawnTime = itemData.RespawnTime;
                    em.SetComponentData(itemEntity, itemComp);

                    if (!em.HasComponent<StoredTag>(itemEntity) && em.HasComponent<LocalTransform>(itemEntity))
                    {
                        var t = em.GetComponentData<LocalTransform>(itemEntity);
                        t.Position = itemData.Position;
                        em.SetComponentData(itemEntity, t);
                    }

                    if (itemData.IsLootGenerated && !em.HasComponent<LootGeneratedTag>(itemEntity))
                        em.AddComponent<LootGeneratedTag>(itemEntity);
                    else if (!itemData.IsLootGenerated && em.HasComponent<LootGeneratedTag>(itemEntity))
                        em.RemoveComponent<LootGeneratedTag>(itemEntity);

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
                                    SlotIndex = i, DataId = s.ItemId,
                                    ContentType = string.IsNullOrEmpty(s.ItemId) ? SlotContentType.Empty : (SlotContentType)s.ContentType,
                                    Amount = s.Amount, ItemEntity = childEntity,
                                    EquipSlot = s.EquipSlot, ContainerType = s.ContainerType,
                                };
                            }
                        }
                    }
                }
                else
                {
                    itemEntity = em.CreateEntity();
                    em.AddComponentData(itemEntity, LocalTransform.FromPosition(itemData.Position));
                    em.AddComponentData(itemEntity, new ItemComponent
                    {
                        Uid = int.Parse(itemData.Uid), ItemId = itemData.ItemId, Amount = itemData.Amount,
                        LootTableId = itemData.LootTableId, RespawnTime = itemData.RespawnTime,
                    });

                    if (itemData.IsLootGenerated) em.AddComponent<LootGeneratedTag>(itemEntity);

                    if (itemData.ContainerSlots.Count > 0)
                    {
                        int totalSlots = itemData.ContainerSlots.Count;
                        Entity bagEntity = em.CreateEntity();
                        em.AddComponentData(bagEntity, new ContainerConfigComponent { Owner = itemEntity, Columns = totalSlots, Rows = 1 });
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
                                SlotIndex = i, DataId = s.ItemId,
                                ContentType = string.IsNullOrEmpty(s.ItemId) ? SlotContentType.Empty : (SlotContentType)s.ContentType,
                                Amount = s.Amount, ItemEntity = childEntity,
                                EquipSlot = s.EquipSlot, ContainerType = s.ContainerType,
                            });
                        }

                        em.AddComponentData(itemEntity, new BuffersLinkComponent { Inventory = bagEntity, Paperdoll = Entity.Null });
                        em.AddComponent<ContainerTag>(itemEntity);
                    }

                    var prefab = Resources.Load<GameObject>("Items/default_item");
                    if (prefab != null)
                    {
                        var go = Object.Instantiate(prefab, itemData.Position, Quaternion.identity);
                        var view = go.GetComponent<ItemView>();
                        if (view != null)
                        {
                            view.uid = itemData.Uid; view.itemId = itemData.ItemId;
                            view.entity = itemEntity; view.IsLinked = true;
                        }
                    }

                    uidToItemEntity[itemData.Uid] = itemEntity;
                }
            }

            // 6. ВОССТАНАВЛИВАЕМ ЮНИТОВ
            foreach (var unitData in saveData.Units)
            {
                if (uidToUnit.TryGetValue(unitData.Uid, out Entity entity))
                {
                    RestoreUnit(entity, unitData, em, uidToItemEntity);
                }
                else
                {
                    Entity spawnMarker = em.CreateEntity();
                    em.AddComponentData(spawnMarker, new UnitSpawnMarkerComponent
                    {
                        PrefabEntity = Entity.Null,
                        UnitId = unitData.UnitId,
                        SpawnPosition = unitData.Position,
                        IsPlayer = unitData.IsPlayer,
                        Level = unitData.Level,
                        Rank = unitData.DefaultRank ?? "common",
                        UniqueNameKey = unitData.UniqueNameKey ?? "",
                    });
                }
            }

            Debug.Log($"📂 Load: юнитов {saveData.Units.Count}, предметов {saveData.Items.Count}");
        }

        // ================================================================
        // RESTORE UNIT
        // ================================================================
        private void RestoreUnit(Entity entity, UnitSaveData data, EntityManager em, Dictionary<string, Entity> uidToItemEntity)
        {
            // Позиция
            var transform = em.GetComponentData<LocalTransform>(entity);
            transform.Position = data.Position;
            em.SetComponentData(entity, transform);

            // Юнит
            if (em.HasComponent<UnitComponent>(entity))
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                unit.Level = data.Level;
                if (!string.IsNullOrEmpty(data.NameKey)) unit.NameKey = data.NameKey;
                if (!string.IsNullOrEmpty(data.Race)) unit.Race = data.Race;
                if (!string.IsNullOrEmpty(data.Type)) unit.Type = data.Type;
                if (!string.IsNullOrEmpty(data.DefaultRank)) unit.DefaultRank = data.DefaultRank;
                em.SetComponentData(entity, unit);
            }

            // Health
            if (em.HasComponent<HealthComponent>(entity))
            {
                var health = em.GetComponentData<HealthComponent>(entity);
                health.Current = (int)data.Health;
                health.Max = (int)data.MaxHealth;
                em.SetComponentData(entity, health);
            }

            // Resource
            if (em.HasComponent<ResourceComponent>(entity))
            {
                var resources = em.GetComponentData<ResourceComponent>(entity);
                resources.Current = data.Mana;
                resources.Max = data.MaxMana;
                em.SetComponentData(entity, resources);
            }

            // IsDead
            if (data.IsDead && !em.HasComponent<IsDeadTag>(entity))
                em.AddComponent<IsDeadTag>(entity);
            else if (!data.IsDead && em.HasComponent<IsDeadTag>(entity))
                em.RemoveComponent<IsDeadTag>(entity);

            // Базовые статы
            if (data.BaseAttributes != null && em.HasComponent<UnitBaseAttributesComponent>(entity))
            {
                em.SetComponentData(entity, new UnitBaseAttributesComponent
                {
                    strength = data.BaseAttributes.Strength, agility = data.BaseAttributes.Agility,
                    intellect = data.BaseAttributes.Intellect, wisdom = data.BaseAttributes.Wisdom,
                    stamina = data.BaseAttributes.Stamina,
                });
            }

            // Прогрессия
            if (data.Progression != null && em.HasComponent<UnitProgressionComponent>(entity))
            {
                var prog = em.GetComponentData<UnitProgressionComponent>(entity);
                prog.Level = data.Progression.Level;
                prog.ExperienceCurrent = data.Progression.ExperienceCurrent;
                prog.ExperienceRequired = data.Progression.ExperienceRequired;
                prog.ExperienceReward = data.Progression.ExperienceReward;
                prog.Rank = System.Enum.Parse<UnitRank>(data.Progression.Rank);
                prog.GrowthHealth = data.Progression.GrowthHealth;
                prog.GrowthDamage = data.Progression.GrowthDamage;
                em.SetComponentData(entity, prog);
            }

            // Инвентарь, кукла, экшенбар
            if (em.HasComponent<BuffersLinkComponent>(entity))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(entity);
                RestoreSlots(em, links.Inventory, data.InventorySlots, uidToItemEntity);
                RestoreSlots(em, links.Paperdoll, data.PaperdollSlots, uidToItemEntity);
                RestoreAuras(em, links.AuraFrame, data.AuraSlots);
            }

            RestoreActionBar(em, entity, data.ActionBarSlots);
            RestoreQuests(em, entity, data.Quests);

            // LeaderTag
            if (data.IsLeader)
                em.AddComponent<LeaderTag>(entity);
            else if (em.HasComponent<LeaderTag>(entity))
                em.RemoveComponent<LeaderTag>(entity);
        }

        // ================================================================
        // ХЕЛПЕРЫ
        // ================================================================
        private void RestoreSlots(EntityManager em, Entity container, List<ItemSlotSaveData> slotsData, Dictionary<string, Entity> uidToItemEntity)
        {
            if (container == Entity.Null || !em.HasBuffer<ItemSlot>(container)) return;
            var slots = em.GetBuffer<ItemSlot>(container);

            for (int i = 0; i < slots.Length && i < slotsData.Count; i++)
            {
                var s = slotsData[i];
                Entity itemEnt = Entity.Null;
                if (!string.IsNullOrEmpty(s.Uid) && uidToItemEntity.TryGetValue(s.Uid, out var ie)) itemEnt = ie;

                slots[i] = new ItemSlot
                {
                    SlotIndex = i, 
                    DataId = s.ItemId,
                    ContentType = string.IsNullOrEmpty(s.ItemId) ? SlotContentType.Empty : (SlotContentType)s.ContentType,
                    Amount = s.Amount, ItemEntity = itemEnt,
                    EquipSlot = s.EquipSlot, ContainerType = s.ContainerType,
                };
            }
        }

        private void RestoreActionBar(EntityManager em, Entity entity, List<ActionBarSaveData> barData)
        {
            if (!em.HasBuffer<ActionBarSlot>(entity) || barData.Count == 0) return;
            var barSlots = em.GetBuffer<ActionBarSlot>(entity);
            for (int k = 0; k < barSlots.Length && k < barData.Count; k++)
            {
                var targetBar = barSlots[k];
                targetBar.AbilityId = barData[k].AbilityId;
                targetBar.ContentType = (SlotContentType)barData[k].ContentType;
                barSlots[k] = targetBar;
            }
        }

        private void RestoreQuests(EntityManager em, Entity entity, List<QuestSaveData> questsData)
        {
            if (!em.HasBuffer<QuestComponent>(entity)) return;
            if (!em.HasBuffer<QuestObjectiveComponent>(entity)) return;

            var quests = em.GetBuffer<QuestComponent>(entity);
            var objectives = em.GetBuffer<QuestObjectiveComponent>(entity);

            quests.Clear();
            objectives.Clear();

            foreach (var qData in questsData)
            {
                quests.Add(new QuestComponent
                {
                    QuestId = qData.QuestId, State = qData.State,
                    IsTracked = qData.IsTracked, RewardClaimed = qData.RewardClaimed,
                });

                foreach (var objData in qData.Objectives)
                {
                    objectives.Add(new QuestObjectiveComponent
                    {
                        QuestId = qData.QuestId, ObjectiveId = objData.ObjectiveId,
                        Current = objData.Current, Required = objData.Required, IsCompleted = objData.IsCompleted,
                    });
                }
            }
        }

        private void RestoreAuras(EntityManager em, Entity auraFrame, List<AuraSlotSaveData> auraSlotsData)
        {
            if (auraFrame == Entity.Null || !em.HasBuffer<AuraSlot>(auraFrame)) return;
            var auraSlots = em.GetBuffer<AuraSlot>(auraFrame);

            for (int i = 0; i < auraSlots.Length && i < auraSlotsData.Count; i++)
            {
                var a = auraSlotsData[i];
                auraSlots[i] = new AuraSlot
                {
                    AuraId = a.AuraId, TimeRemaining = a.TimeRemaining,
                    Duration = a.Duration, Stacks = a.Stacks,
                    CasterEntity = Entity.Null, TargetEntity = Entity.Null,
                };
            }
        }
    }

    // ================================================================
    // СТРУКТУРЫ ДАННЫХ
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
        public string NameKey;
        public string UniqueNameKey;
        public string Race;
        public string Type;
        public string DefaultRank;
        public int Level;
        public float3 Position;
        public float Health;
        public float MaxHealth;
        public float Mana;
        public float MaxMana;
        public bool IsDead;
        public bool IsPlayer;
        public bool IsLeader;
        public bool IsMonster;
        public bool IsNpc;

        public BaseAttributesSaveData BaseAttributes;
        public ProgressionSaveData Progression;

        public List<ItemSlotSaveData> InventorySlots = new();
        public List<ItemSlotSaveData> PaperdollSlots = new();
        public List<ActionBarSaveData> ActionBarSlots = new();
        public List<QuestSaveData> Quests = new();
        public List<AuraSlotSaveData> AuraSlots = new();
    }

    [System.Serializable]
    public class BaseAttributesSaveData
    {
        public int Strength, Agility, Intellect, Wisdom, Stamina;
    }

    [System.Serializable]
    public class ProgressionSaveData
    {
        public int Level;
        public int ExperienceCurrent;
        public int ExperienceRequired;
        public int ExperienceReward;
        public string Rank;
        public float GrowthHealth;
        public float GrowthDamage;
    }

    [System.Serializable]
    public class QuestSaveData
    {
        public string QuestId;
        public QuestState State;
        public bool IsTracked;
        public bool RewardClaimed;
        public List<ObjectiveSaveData> Objectives = new();
    }

    [System.Serializable]
    public class ObjectiveSaveData
    {
        public string ObjectiveId;
        public int Current;
        public int Required;
        public bool IsCompleted;
    }

    [System.Serializable]
    public class AuraSlotSaveData
    {
        public string AuraId;
        public float TimeRemaining;
        public float Duration;
        public int Stacks;
    }

    [System.Serializable]
    public class ItemSlotSaveData
    {
        public string Uid;
        public string ItemId;
        public int Amount;
        public EquipSlot EquipSlot;
        public ContainerType ContainerType;
        public byte ContentType; // 🎯 Сохраняем енам как байт
    }

    [System.Serializable]
    public class ActionBarSaveData
    {
        public int SlotIndex;
        public string AbilityId;
        public byte ContentType; // 🎯 Сохраняем енам как байт
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

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.Core.Quest;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ActionDispatcherSystem))]
    public partial class QuestSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;
        private EntityQuery _acceptQuery;
        private EntityQuery _completeQuery;
        private EntityQuery _trackQuery;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _acceptQuery = GetEntityQuery(ComponentType.ReadOnly<QuestAcceptRequest>());
            _completeQuery = GetEntityQuery(ComponentType.ReadOnly<QuestCompleteRequest>());
            _trackQuery = GetEntityQuery(ComponentType.ReadOnly<QuestTrackRequest>());
        }

        protected override void OnUpdate()
        {
            if (_acceptQuery.IsEmpty && _completeQuery.IsEmpty && _trackQuery.IsEmpty) return;

            var em = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();

            var acceptedThisFrame = new NativeHashSet<(Entity, FixedString64Bytes)>(16, Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestAcceptRequest>>().WithEntityAccess())
            {
                var key = (req.ValueRO.Unit, req.ValueRO.QuestId);
                if (acceptedThisFrame.Contains(key))
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }
                HandleAccept(req.ValueRO.Unit, req.ValueRO.QuestId, em, ecb);
                acceptedThisFrame.Add(key);
                ecb.DestroyEntity(reqEntity);
            }

            acceptedThisFrame.Dispose();

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestCompleteRequest>>().WithEntityAccess())
            {
                HandleComplete(req.ValueRO.Unit, req.ValueRO.QuestId, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestTrackRequest>>().WithEntityAccess())
            {
                HandleTrack(req.ValueRO.Unit, req.ValueRO.QuestId, req.ValueRO.Track, em);
                ecb.DestroyEntity(reqEntity);
            }

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private void HandleAccept(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit) || !em.HasBuffer<QuestObjectiveComponent>(unit)) return;

            var qBuffer = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < qBuffer.Length; i++)
            {
                if (qBuffer[i].QuestId == questId)
                {
                    Debug.Log($"[QuestSystem] Квест '{questId}' уже в буфере — игнор.");
                    return;
                }
            }

            var config = QuestsDatabase.GetQuest(questId.ToString());
            if (config == null) return;

            ecb.AppendToBuffer(unit, new QuestComponent
            {
                QuestId = questId,
                State = QuestState.Active,
                IsTracked = true,
                RewardClaimed = false
            });

            if (config.objectives != null)
            {
                foreach (var obj in config.objectives)
                {
                    ecb.AppendToBuffer(unit, new QuestObjectiveComponent
                    {
                        QuestId = questId,
                        ObjectiveId = obj.id,
                        Current = 0,
                        Required = obj.required_amount,
                        IsCompleted = false
                    });
                }
            }
        }

        private void HandleComplete(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit)) return;

            var qBuffer = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < qBuffer.Length; i++)
            {
                if (qBuffer[i].QuestId != questId) continue;

                if (qBuffer[i].State != QuestState.Completed)
                {
                    Debug.LogWarning($"[QuestSystem] Квест '{questId}' не выполнен (State={qBuffer[i].State}).");
                    return;
                }

                if (qBuffer[i].RewardClaimed)
                {
                    Debug.LogWarning($"[QuestSystem] Награда за '{questId}' уже получена.");
                    return;
                }

                var config = QuestsDatabase.GetQuest(questId.ToString());
                if (config != null && config.objectives != null)
                {
                    // 🎯 1. Проверка предметов
                    if (!CheckCollectItems(unit, config, em)) return;

                    // 🎯 2. Получаем собеседника из активного диалога
                    Entity speaker = GetDialogueSpeaker(unit, em);

                    // 🎯 3. Резолвим его инвентарь (чтобы редюсер знал, куда класть предметы)
                    Entity targetInventory = Entity.Null;
                    if (speaker != Entity.Null && em.HasComponent<BuffersLinkComponent>(speaker))
                    {
                        targetInventory = em.GetComponentData<BuffersLinkComponent>(speaker).Inventory;
                    }

                    // 🎯 4. Создание команд на трансфер (если есть collect и известен инвентарь цели)
                    if (targetInventory != Entity.Null)
                    {
                        foreach (var objCfg in config.objectives)
                        {
                            if (objCfg.type != QuestObjectiveType.Collect) continue;
                            string itemId = objCfg.identity?["name_key"];
                            if (string.IsNullOrEmpty(itemId)) continue;

                            int remaining = objCfg.required_amount;
                            if (em.HasComponent<BuffersLinkComponent>(unit))
                            {
                                var links = em.GetComponentData<BuffersLinkComponent>(unit);
                                if (links.Inventory != Entity.Null && em.HasBuffer<ItemSlot>(links.Inventory))
                                {
                                    var slots = em.GetBuffer<ItemSlot>(links.Inventory);
                                    for (int s = 0; s < slots.Length && remaining > 0; s++)
                                    {
                                        if (slots[s].IsEmpty || slots[s].DataId.ToString() != itemId) continue;

                                        int toTake = math.min(slots[s].Amount, remaining);
                                        
                                        var cmdEntity = ecb.CreateEntity();
                                        
                                        // 🎯 Если в слоте ровно столько же (или меньше), чем нужно — переносим весь стак.
                                        // Если больше — отрезаем нужное количество через Split.
                                        ActionKind action = (slots[s].Amount <= toTake) 
                                            ? ActionKind.ItemTransfer 
                                            : ActionKind.ItemSplit;

                                        ecb.AddComponent(cmdEntity, new ActionCommand
                                        {
                                            Action = action,
                                            SourceEntity = links.Inventory,            // Откуда (игрок)
                                            SourceSlot = s,
                                            TargetEntity = targetInventory, // 🎯 Куда (инвентарь собеседника)
                                            TargetSlot = -1,                // Авто-поиск слота
                                            ItemId = itemId,
                                            Amount = toTake
                                        });
                                        
                                        remaining -= toTake;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[QuestSystem] Квест '{questId}' сдан, но диалог не активен или у собеседника нет инвентаря. Предметы не переданы.");
                    }
                }

                // 🎯 5. ОТПРАВЛЯЕМ СОБЫТИЕ ДЛЯ ПОПАПА (награды)
                if (SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
                {
                    var buf = em.GetBuffer<QuestReadyToClaimEvent>(gameEventsEntity);
                    buf.Add(new QuestReadyToClaimEvent { Unit = unit, QuestId = questId });
                    Debug.Log($"[QuestSystem] Квест '{questId}' готов к сдаче, событие отправлено.");
                }
                else
                {
                    Debug.LogError("[QuestSystem] Не найдена шина GameEvent!");
                }

                return;
            }

            Debug.LogWarning($"[QuestSystem] Попытка сдать '{questId}', которого нет у юнита.");
        }

        private bool CheckCollectItems(Entity unit, QuestConfig config, EntityManager em)
        {
            if (!em.HasComponent<BuffersLinkComponent>(unit)) return true;
            var links = em.GetComponentData<BuffersLinkComponent>(unit);
            if (links.Inventory == Entity.Null || !em.HasBuffer<ItemSlot>(links.Inventory)) return true;

            var slots = em.GetBuffer<ItemSlot>(links.Inventory);

            foreach (var objCfg in config.objectives)
            {
                if (objCfg.type != QuestObjectiveType.Collect) continue;
                string itemId = objCfg.identity?["name_key"];
                if (string.IsNullOrEmpty(itemId)) continue;

                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsEmpty && slots[i].DataId.ToString() == itemId)
                        count += slots[i].Amount;
                }

                if (count < objCfg.required_amount)
                {
                    Debug.LogWarning($"[QuestSystem] Не хватает {itemId} для сдачи (нужно {objCfg.required_amount}, есть {count}).");
                    return false;
                }
            }
            return true;
        }

        private void HandleTrack(Entity unit, FixedString64Bytes questId, bool track, EntityManager em)
        {
            if (!em.HasBuffer<QuestComponent>(unit)) return;
            var quests = em.GetBuffer<QuestComponent>(unit);

            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                bool shouldAffect = questId.IsEmpty || q.QuestId == questId;
                if (!shouldAffect || q.IsTracked == track) continue;

                q.IsTracked = track;
                quests[i] = q;
            }
        }

        // 🎯 Получаем NPC из текущего диалога (без хардкода)
        private Entity GetDialogueSpeaker(Entity playerEntity, EntityManager em)
        {
            if (em.HasComponent<DialogueStateComponent>(playerEntity))
            {
                var dialogueState = em.GetComponentData<DialogueStateComponent>(playerEntity);
                if (dialogueState.IsActive && dialogueState.Speaker != Entity.Null)
                {
                    return dialogueState.Speaker;
                }
            }
            return Entity.Null;
        }
    }
}

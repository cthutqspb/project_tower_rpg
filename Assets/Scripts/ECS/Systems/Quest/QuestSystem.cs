using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
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
            if (_acceptQuery.IsEmpty && _completeQuery.IsEmpty && _trackQuery.IsEmpty)
                return;

            var em = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();

            // 🛡️ ЗАЩИТА ОТ ДУБЛИКАТОВ ВНУТРИ КАДРА
            // Храним пары "юнит + квест", которые уже обработали в этом кадре
            var acceptedThisFrame = new NativeHashSet<(Entity, FixedString64Bytes)>(16, Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestAcceptRequest>>().WithEntityAccess())
            {
                var key = (req.ValueRO.Unit, req.ValueRO.QuestId);
                
                // Если этот же юнит уже принял этот же квест в этом кадре — пропускаем
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
                HandleTrack(req.ValueRO.Unit, req.ValueRO.QuestId, em);
                ecb.DestroyEntity(reqEntity);
            }

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private void HandleAccept(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit)) return;
            if (!em.HasBuffer<QuestObjectiveComponent>(unit)) return;

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
            if (config == null)
            {
                Debug.LogWarning($"[QuestSystem] Нет конфига для '{questId}' — квест не принят.");
                return;
            }

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

            Debug.Log($"[QuestSystem] Квест '{questId}' принят юнитом {unit.Index}. Целей: {config.objectives?.Count ?? 0}");
        }

        private void HandleComplete(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit)) return;

            var qBuffer = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < qBuffer.Length; i++)
            {
                if (qBuffer[i].QuestId == questId)
                {
                    if (qBuffer[i].State != QuestState.Completed)
                    {
                        Debug.LogWarning($"[QuestSystem] Квест '{questId}' не выполнен (State={qBuffer[i].State}), сдача отклонена.");
                        return;
                    }

                    if (SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
                    {
                        var buf = em.GetBuffer<QuestReadyToClaimEvent>(gameEventsEntity);
                        buf.Add(new QuestReadyToClaimEvent { Unit = unit, QuestId = questId });
                        Debug.Log($"[QuestSystem] Квест '{questId}' готов к сдаче, событие отправлено.");
                    }
                    else
                    {
                        Debug.LogError($"[QuestSystem] Не найдена шина GameEvent!");
                    }

                    return;
                }
            }

            Debug.LogWarning($"[QuestSystem] Попытка сдать '{questId}', которого нет у юнита.");
        }

        private void HandleTrack(Entity unit, FixedString64Bytes questId, EntityManager em)
        {
            if (!em.HasBuffer<QuestComponent>(unit)) return;
            var quests = em.GetBuffer<QuestComponent>(unit);

            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                bool newTracked = (q.QuestId == questId);

                // 🧊 Пишем только при реальном изменении — иначе зря помечаем буфер dirty
                if (q.IsTracked == newTracked) continue;

                q.IsTracked = newTracked;
                quests[i] = q;
            }
        }
    }
}

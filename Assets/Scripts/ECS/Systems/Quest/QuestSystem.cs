using Unity.Collections;
using Unity.Entities;
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

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();

            var acceptList = new NativeList<Entity>(Allocator.Temp);
            var completeList = new NativeList<Entity>(Allocator.Temp);
            var trackList = new NativeList<Entity>(Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestAcceptRequest>>().WithEntityAccess())
                acceptList.Add(reqEntity);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestCompleteRequest>>().WithEntityAccess())
                completeList.Add(reqEntity);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestTrackRequest>>().WithEntityAccess())
                trackList.Add(reqEntity);

            foreach (var reqEntity in acceptList)
            {
                var req = em.GetComponentData<QuestAcceptRequest>(reqEntity);
                HandleAccept(req.Unit, req.QuestId, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            foreach (var reqEntity in completeList)
            {
                var req = em.GetComponentData<QuestCompleteRequest>(reqEntity);
                HandleComplete(req.Unit, req.QuestId, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            foreach (var reqEntity in trackList)
            {
                var req = em.GetComponentData<QuestTrackRequest>(reqEntity);
                HandleTrack(req.Unit, req.QuestId, em);
                ecb.DestroyEntity(reqEntity);
            }

            acceptList.Dispose();
            completeList.Dispose();
            trackList.Dispose();
            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        // ================================================================
        // ПРИНЯТИЕ КВЕСТА
        // ================================================================
        private void HandleAccept(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit)) return;
            if (!em.HasBuffer<QuestObjectiveComponent>(unit)) return;

            // Защита от дубля
            var qBuffer = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < qBuffer.Length; i++)
            {
                if (qBuffer[i].QuestId == questId)
                {
                    Debug.Log($"[QuestSystem] Квест '{questId}' уже в буфере — игнор.");
                    return;
                }
            }

            // Получаем конфиг
            var config = QuestsDatabase.GetQuest(questId.ToString());
            if (config == null)
            {
                Debug.LogWarning($"[QuestSystem] Нет конфига для '{questId}' — квест не принят.");
                return;
            }

            // Записываем сам квест
            ecb.AppendToBuffer(unit, new QuestComponent
            {
                QuestId = questId,
                State = QuestState.Active,
                IsTracked = true
            });

            // Записываем цели
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

        // ================================================================
        // СДАЧА КВЕСТА
        // ================================================================
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

                    var q = qBuffer[i];
                    q.State = QuestState.TurnedIn;
                    qBuffer[i] = q;

                    Debug.Log($"[QuestSystem] Квест '{questId}' сдан юнитом {unit.Index}.");

                    GrantRewards(unit, questId, em, ecb);

                    return;
                }
            }

            Debug.LogWarning($"[QuestSystem] Попытка сдать '{questId}', которого нет у юнита.");
        }

        private void GrantRewards(Entity unit, FixedString64Bytes questId, EntityManager em, EntityCommandBuffer ecb)
        {
            var config = QuestsDatabase.GetQuest(questId.ToString());
            if (config?.rewards == null || config.rewards.Count == 0) return;

            foreach (var reward in config.rewards)
            {
                var cmdEntity = ecb.CreateEntity();
                ecb.AddComponent(cmdEntity, new ActionCommand
                {
                    Action = ItemActions.Create,
                    TargetEntity = unit,
                    ItemId = reward.item_id,
                    Amount = reward.amount
                });
            }

            Debug.Log($"[QuestSystem] Квест '{questId}' сдан — {config.rewards.Count} награды отправлены в диспетчер.");
        }

        private void HandleTrack(Entity unit, FixedString64Bytes questId, EntityManager em)
        {
            if (!em.HasBuffer<QuestComponent>(unit)) return;
            var quests = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                q.IsTracked = (q.QuestId == questId);
                quests[i] = q;
            }
        }
    }
}

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

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestAcceptRequest>>().WithEntityAccess())
                acceptList.Add(reqEntity);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestCompleteRequest>>().WithEntityAccess())
                completeList.Add(reqEntity);

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

            acceptList.Dispose();
            completeList.Dispose();
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
                Progress = 0
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
                    var q = qBuffer[i];
                    q.State = QuestState.Completed;
                    qBuffer[i] = q;

                    Debug.Log($"[QuestSystem] Квест '{questId}' сдан юнитом {unit.Index}.");

                    // TODO: Выдача награды.
                    // 1. Взять config = QuestsDatabase.GetQuest(questId)
                    // 2. Для каждого reward в config.rewards:
                    //    - создать сущность предмета через ItemSpawnSystem / отдельный SpawnRewardItemRequest
                    //    - положить в инвентарь юнита через ItemReducer.AddItem / Transfer
                    // Пока заглушка.

                    return;
                }
            }

            Debug.LogWarning($"[QuestSystem] Попытка сдать '{questId}', которого нет у юнита.");
        }
    }
}

using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using UnityEngine;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitSpawnSystem))]
    public partial class QuestAnchorSystem : SystemBase
    {
        private bool _isLinked = false;

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 1. Одноразовая линковка: связываем квесты с якорями
            if (!_isLinked)
            {
                TryLinkAnchors(em);
            }

            // 2. Каждый кадр обновляем маркеры (!) у якорей (дёшево)
            UpdateAnchorMarks(em);
        }

        // ================================================================
        // ЛИНКОВКА: КВЕСТ ↔ ЯКОРЬ
        // ================================================================
        private void TryLinkAnchors(EntityManager em)
        {
            // Проходим по всем юнитам (у которых есть имя)
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadWrite<NameplateStateComponent>()
            );

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                string nameKey = unit.NameKey.ToString();

                var questsByGiver = QuestsDatabase.GetQuestsByGiver(nameKey);

                if (questsByGiver.Count > 0)
                {
                    var buffer = em.AddBuffer<QuestAnchor>(entity);
                    foreach (var q in questsByGiver)
                    {
                        buffer.Add(new QuestAnchor { QuestId = q.id });
                    }

                    Debug.Log($"[QuestAnchorSystem] Юнит {nameKey} связан с {questsByGiver.Count} квестами.");
                }
            }

            entities.Dispose();
            _isLinked = true;
        }

        // ================================================================
        // ОБНОВЛЕНИЕ МАРКЕРОВ (!)
        // ================================================================
        private void UpdateAnchorMarks(EntityManager em)
        {
            // Найти игрока
            using var playerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<QuestComponent>());
            if (playerQuery.IsEmpty) return;

            Entity playerEntity = playerQuery.GetSingletonEntity();
            var playerQuests = em.GetBuffer<QuestComponent>(playerEntity);

            // Все якоря
            using var anchorQuery = em.CreateEntityQuery(
                ComponentType.ReadWrite<NameplateStateComponent>(),
                ComponentType.ReadOnly<QuestAnchor>()
            );

            if (anchorQuery.IsEmpty) return;

            var anchors = anchorQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchorEntity = anchors[i];
                var anchorQuests = em.GetBuffer<QuestAnchor>(anchorEntity);

                NameplateMark mark = NameplateMark.None;

                for (int a = 0; a < anchorQuests.Length; a++)
                {
                    var questId = anchorQuests[a].QuestId;
                    QuestState state = QuestState.Locked;

                    for (int q = 0; q < playerQuests.Length; q++)
                    {
                        if (playerQuests[q].QuestId == questId)
                        {
                            state = playerQuests[q].State;
                            break;
                        }
                    }

                    if (state == QuestState.Locked)
                    {
                        mark = NameplateMark.Quest;   // ! — можно взять
                        break;
                    }
                    else if (state == QuestState.Completed)
                    {
                        mark = NameplateMark.TurnIn;  // ? — можно сдать
                        // не break — если есть ещё Locked, приоритет у него
                    }
                }

                var nameplate = em.GetComponentData<NameplateStateComponent>(anchorEntity);
                nameplate.Mark = mark;
                em.SetComponentData(anchorEntity, nameplate);
            }
            anchors.Dispose();
        }
    }
}

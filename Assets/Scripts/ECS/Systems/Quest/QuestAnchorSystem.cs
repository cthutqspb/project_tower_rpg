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

        // 🎯 Кэшируем запросы в OnCreate (или Lazy-инициализации)
        private EntityQuery _anchorQuery;
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _anchorQuery = GetEntityQuery(
                ComponentType.ReadWrite<NameplateStateComponent>(),
                ComponentType.ReadOnly<QuestAnchor>()
            );

            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<QuestComponent>()
            );
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 1. Одноразовая линковка
            if (!_isLinked)
            {
                TryLinkAnchors(em);
            }

            // 2. Каждый кадр обновляем маркеры (дёшево)
            UpdateAnchorMarks(em);
        }

        private void TryLinkAnchors(EntityManager em)
        {
            // Создаем запрос один раз для линковки (это не каждый кадр, можно)
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadWrite<NameplateStateComponent>()
            );

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var unit = em.GetComponentData<UnitComponent>(entity);
                // ToString() тут нормален — выполняется 1 раз при старте
                string nameKey = unit.NameKey.ToString(); 

                var questsByGiver = QuestsDatabase.GetQuestsByGiver(nameKey);

                if (questsByGiver.Count > 0)
                {
                    // Избегаем ошибки "буфер уже существует"
                    if (!em.HasBuffer<QuestAnchor>(entity))
                    {
                        var buffer = em.AddBuffer<QuestAnchor>(entity);
                        foreach (var q in questsByGiver)
                        {
                            buffer.Add(new QuestAnchor { QuestId = q.id });
                        }
                    }
                    
                    Debug.Log($"[QuestAnchorSystem] Юнит {nameKey} связан с {questsByGiver.Count} квестами.");
                }
            }

            entities.Dispose();
            _isLinked = true;
        }

        private void UpdateAnchorMarks(EntityManager em)
        {
            if (_playerQuery.IsEmpty || _anchorQuery.IsEmpty) return;

            Entity playerEntity = _playerQuery.GetSingletonEntity();
            var playerQuests = em.GetBuffer<QuestComponent>(playerEntity);

            // 🎯 Идем напрямую по чанкам через SystemAPI.Query (без ToEntityArray)
            foreach (var (nameplateRW, anchorBuffer) in SystemAPI.Query<RefRW<NameplateStateComponent>, DynamicBuffer<QuestAnchor>>())
            {
                NameplateMark mark = NameplateMark.None;

                for (int a = 0; a < anchorBuffer.Length; a++)
                {
                    var questId = anchorBuffer[a].QuestId;
                    QuestState state = QuestState.Locked;

                    for (int q = 0; q < playerQuests.Length; q++)
                    {
                        // 🎯 Сравнение FixedString == FixedString (быстро, без аллокаций)
                        if (playerQuests[q].QuestId == questId)
                        {
                            state = playerQuests[q].State;
                            break;
                        }
                    }

                    if (state == QuestState.Locked)
                    {
                        mark = NameplateMark.Quest;   // ! — можно взять
                        break; // Приоритет у Locked
                    }
                    else if (state == QuestState.Completed)
                    {
                        mark = NameplateMark.TurnIn;  // ? — можно сдать
                        // Не break — вдруг есть еще Locked, у него приоритет
                    }
                }

                if (nameplateRW.ValueRO.Mark != mark)
                {
                    nameplateRW.ValueRW.Mark = mark;
                }
            }
        }
    }
}

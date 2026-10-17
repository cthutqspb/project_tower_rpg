using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    [UpdateBefore(typeof(GameEventCleanupSystem))]
    public partial class QuestProgressSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
                return;

            var killEvents = em.GetBuffer<EntityKilledEvent>(gameEventsEntity);
            if (killEvents.Length == 0) return;

            foreach (var (_, playerEntity) in
                     SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
            {
                if (!em.HasBuffer<QuestObjectiveComponent>(playerEntity)) continue;
                if (!em.HasBuffer<QuestComponent>(playerEntity)) continue;

                var objectives = em.GetBuffer<QuestObjectiveComponent>(playerEntity);

                // 1. Матчим события с целями
                for (int e = 0; e < killEvents.Length; e++)
                {
                    var evt = killEvents[e];

                    // Считаем только убийства игроком
                    if (evt.Source != playerEntity) continue;

                    for (int i = 0; i < objectives.Length; i++)
                    {
                        var obj = objectives[i];
                        if (obj.IsCompleted) continue;

                        var cfg = QuestsDatabase.GetQuest(obj.QuestId.ToString());
                        if (cfg == null || cfg.objectives == null) continue;

                        QuestObjective objCfg = null;
                        for (int o = 0; o < cfg.objectives.Count; o++)
                        {
                            if (cfg.objectives[o].id == obj.ObjectiveId.ToString())
                            {
                                objCfg = cfg.objectives[o];
                                break;
                            }
                        }
                        if (objCfg == null) continue;
                        if (objCfg.type != "kill") continue;

                        if (!MatchFilter(objCfg.identity, evt)) continue;

                        obj.Current++;
                        if (obj.Current >= obj.Required) obj.IsCompleted = true;
                        objectives[i] = obj;
                    }
                }

                // 2. Проверяем, все ли цели квестов выполнены → переводим State
                var quests = em.GetBuffer<QuestComponent>(playerEntity);
                for (int q = 0; q < quests.Length; q++)
                {
                    if (quests[q].State != QuestState.Active) continue;

                    bool allDone = true;
                    bool hasAny = false;

                    for (int i = 0; i < objectives.Length; i++)
                    {
                        if (objectives[i].QuestId != quests[q].QuestId) continue;
                        hasAny = true;
                        if (!objectives[i].IsCompleted) { allDone = false; break; }
                    }

                    if (hasAny && allDone)
                    {
                        var quest = quests[q];
                        quest.State = QuestState.Completed;
                        quests[q] = quest;
                        Debug.Log($"[QuestProgressSystem] Квест '{quest.QuestId}' готов к сдаче.");
                    }
                }
            }
        }

        private static bool MatchFilter(
            System.Collections.Generic.Dictionary<string, string> filter,
            EntityKilledEvent evt)
        {
            if (filter == null)
            {
                Debug.LogWarning($"[QuestProgressSystem] Filter is NULL — пропуск цели");
                return false;
            }

            if (filter.Count == 0)
            {
                // Пустой фильтр — цель без ограничений. Матчится со всем.
                // Это осознанное решение, оставляем, но логируем — чтобы не было сюрпризов.
                return true;
            }

            foreach (var pair in filter)
            {
                switch (pair.Key)
                {
                    case "name_key":
                        if (evt.TargetNameKey.ToString() != pair.Value) return false;
                        break;
                    case "race":
                        if (evt.TargetRace.ToString() != pair.Value) return false;
                        break;
                    case "faction":
                        if (evt.TargetFaction.ToString() != pair.Value) return false;
                        break;
                    case "type":
                        if (evt.TargetType.ToString() != pair.Value) return false;
                        break;
                    case "default_rank":
                        if (evt.TargetDefaultRank.ToString() != pair.Value) return false;
                        break;
                    default:
                        Debug.LogWarning($"[QuestProgressSystem] Неизвестный ключ фильтра: '{pair.Key}'");
                        return false;
                }
            }

            return true;
        }
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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

            // 1. Ищем игрока
            Entity playerEntity = Entity.Null;
            foreach (var (_, player) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
            {
                playerEntity = player;
                break;
            }

            if (playerEntity == Entity.Null) return;
            if (!em.HasBuffer<QuestObjectiveComponent>(playerEntity)) return;
            if (!em.HasBuffer<QuestComponent>(playerEntity)) return;

            var objectives = em.GetBuffer<QuestObjectiveComponent>(playerEntity);

            // 2. ОБРАБОТКА УБИЙСТВ (KILL)
            if (SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
            {
                var killEvents = em.GetBuffer<EntityKilledEvent>(gameEventsEntity);
                
                for (int e = 0; e < killEvents.Length; e++)
                {
                    var evt = killEvents[e];
                    if (evt.Source != playerEntity) continue;

                    for (int i = 0; i < objectives.Length; i++)
                    {
                        var obj = objectives[i];
                        if (obj.IsCompleted) continue;

                        var objCfg = GetObjectiveConfig(obj);
                        if (objCfg == null || objCfg.type != QuestObjectiveType.Kill) continue;

                        if (!MatchKillFilter(objCfg.identity, evt)) continue;

                        int newCurrent = obj.Current + 1;
                        bool newCompleted = newCurrent >= obj.Required;

                        // 🎯 ПИШЕМ ТОЛЬКО ЕСЛИ ИЗМЕНИЛОСЬ
                        if (obj.Current != newCurrent || obj.IsCompleted != newCompleted)
                        {
                            obj.Current = newCurrent;
                            obj.IsCompleted = newCompleted;
                            objectives[i] = obj;
                        }
                    }
                }
            }

            // 3. ОБРАБОТКА СБОРА (COLLECT)
            if (em.HasComponent<BuffersLinkComponent>(playerEntity))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(playerEntity);
                Entity inventory = links.Inventory;

                // 🎯 Читаем буфер ТОЛЬКО если он изменился (или если это первый кадр)
                var slotLookup = SystemAPI.GetBufferLookup<ItemSlot>(true);
                if (inventory != Entity.Null && slotLookup.HasBuffer(inventory) && slotLookup.DidChange(inventory, LastSystemVersion))
                {
                    var slots = slotLookup[inventory];

                    for (int i = 0; i < objectives.Length; i++)
                    {
                        var obj = objectives[i];
                        var objCfg = GetObjectiveConfig(obj);
                        if (objCfg == null || objCfg.type != QuestObjectiveType.Collect) continue;

                        string targetItemId = null;
                        if (objCfg.identity != null && objCfg.identity.ContainsKey("name_key"))
                            targetItemId = objCfg.identity["name_key"];

                        if (string.IsNullOrEmpty(targetItemId)) continue;

                        int currentCount = 0;
                        for (int s = 0; s < slots.Length; s++)
                        {
                            if (slots[s].IsEmpty) continue;
                            if (slots[s].DataId.ToString() == targetItemId)
                                currentCount += slots[s].Amount;
                        }

                        int newCurrent = math.min(currentCount, obj.Required);
                        bool newCompleted = newCurrent >= obj.Required;

                        if (obj.Current != newCurrent || obj.IsCompleted != newCompleted)
                        {
                            obj.Current = newCurrent;
                            obj.IsCompleted = newCompleted;
                            objectives[i] = obj;
                        }
                    }
                }
            }

            // 4. ПРОВЕРКА ЗАВЕРШЕНИЯ / ОТКАТА КВЕСТОВ
            var quests = em.GetBuffer<QuestComponent>(playerEntity);
            for (int q = 0; q < quests.Length; q++)
            {
                if (quests[q].State != QuestState.Active && quests[q].State != QuestState.Completed) 
                    continue;

                bool allDone = true;
                bool hasAny = false;

                for (int i = 0; i < objectives.Length; i++)
                {
                    if (objectives[i].QuestId != quests[q].QuestId) continue;
                    hasAny = true;
                    if (!objectives[i].IsCompleted) { allDone = false; break; }
                }

                if (hasAny && allDone && quests[q].State == QuestState.Active)
                {
                    var quest = quests[q];
                    quest.State = QuestState.Completed;
                    quests[q] = quest;
                    Debug.Log($"[QuestProgressSystem] Квест '{quest.QuestId}' готов к сдаче.");
                }
                else if (hasAny && !allDone && quests[q].State == QuestState.Completed)
                {
                    var quest = quests[q];
                    quest.State = QuestState.Active;
                    quests[q] = quest;
                    Debug.Log($"[QuestProgressSystem] Квест '{quest.QuestId}' откатан в Active (цели больше не выполнены).");
                }
            }
        }

        private QuestObjective GetObjectiveConfig(QuestObjectiveComponent obj)
        {
            var cfg = QuestsDatabase.GetQuest(obj.QuestId.ToString());
            if (cfg == null || cfg.objectives == null) return null;

            for (int o = 0; o < cfg.objectives.Count; o++)
            {
                if (cfg.objectives[o].id == obj.ObjectiveId.ToString())
                    return cfg.objectives[o];
            }
            return null;
        }

        private static bool MatchKillFilter(
            System.Collections.Generic.Dictionary<string, string> filter,
            EntityKilledEvent evt)
        {
            if (filter == null || filter.Count == 0) return true;

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
                        return false;
                }
            }

            return true;
        }
    }
}

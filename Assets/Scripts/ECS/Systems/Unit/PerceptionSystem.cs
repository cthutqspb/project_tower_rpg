using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.ECS.Components;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AiSystem))]
    public partial class PerceptionSystem : SystemBase
    {
        private const float LEASH_MULTIPLIER = 1.5f;

        // Логи включаем только раз в N секунд, чтобы не спамить
        private float _lastLogTime = 0f;
        private const float LOG_INTERVAL = 2f;

        protected override void OnUpdate()
        {
            var em = EntityManager;
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            bool doLog = (currentTime - _lastLogTime) >= LOG_INTERVAL;

            if (doLog)
                _lastLogTime = currentTime;

            // ================================================================
            // 1. СОБИРАЕМ ВСЕХ ЖИВЫХ ЮНИТОВ
            // ================================================================
            var aliveQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            );
            var aliveEntities = aliveQuery.ToEntityArray(Allocator.Temp);

            if (doLog)
            {
                Debug.Log($"[Perception] Живых юнитов в мире: {aliveEntities.Length}");

                for (int i = 0; i < aliveEntities.Length; i++)
                {
                    var e = aliveEntities[i];
                    var u = em.GetComponentData<UnitComponent>(e);
                    bool isPlayer = em.HasComponent<PlayerTag>(e);
                    Debug.Log($"  └─ Entity {e.Index}: uid={u.Uid} faction={u.Faction} aggro={u.AggroRadius} player={isPlayer}");
                }
            }

            // ================================================================
            // 2. СОБИРАЕМ ВСЕХ МОБОВ
            // ================================================================
            var mobQuery = em.CreateEntityQuery(
                ComponentType.ReadWrite<AiComponent>(),
                ComponentType.ReadWrite<CombatStateComponent>(),
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            );
            var mobEntities = mobQuery.ToEntityArray(Allocator.Temp);

            if (doLog)
                Debug.Log($"[Perception] Мобов для проверки: {mobEntities.Length}");

            foreach (var mobEntity in mobEntities)
            {
                var ai = em.GetComponentData<AiComponent>(mobEntity);

                if (!ai.IsFromFactory)
                {
                    if (doLog)
                    {
                        var u = em.GetComponentData<UnitComponent>(mobEntity);
                        Debug.Log($"[Perception] Скип {u.Uid}: IsFromFactory=false");
                    }
                    continue;
                }

                var selfUnit = em.GetComponentData<UnitComponent>(mobEntity);
                var selfTransform = em.GetComponentData<LocalTransform>(mobEntity);
                var selfCombat = em.GetComponentData<CombatStateComponent>(mobEntity);

                if (doLog)
                {
                    Debug.Log($"[Perception] Моб {selfUnit.Uid} (E{mobEntity.Index}): faction={selfUnit.Faction}, " +
                              $"aggro={selfUnit.AggroRadius}, pos={selfTransform.Position}, " +
                              $"inCombat={selfCombat.IsInCombat}, currentTarget={selfCombat.CurrentTarget.Index}");
                }

                // ------------------------------------------------------------
                // ФАЗА А: ЕСТЬ ЦЕЛЬ
                // ------------------------------------------------------------
                if (selfCombat.CurrentTarget != Entity.Null)
                {
                    bool targetDead = !em.Exists(selfCombat.CurrentTarget)
                                      || em.HasComponent<IsDeadTag>(selfCombat.CurrentTarget);

                    if (targetDead)
                    {
                        if (doLog)
                            Debug.Log($"[Perception] {selfUnit.Uid}: цель мертва — сбрасываем");

                        selfCombat.CurrentTarget = Entity.Null;
                        selfCombat.IsInCombat = false;
                        em.SetComponentData(mobEntity, selfCombat);
                    }
                    else
                    {
                        float3 toTarget = em.GetComponentData<LocalTransform>(selfCombat.CurrentTarget).Position - selfTransform.Position;
                        toTarget.y = 0f;
                        float dist = math.length(toTarget);
                        float leash = selfUnit.AggroRadius * LEASH_MULTIPLIER;

                        if (doLog)
                            Debug.Log($"[Perception] {selfUnit.Uid}: цель жива, dist={dist:F1}, leash={leash:F1}");

                        if (dist > leash)
                        {
                            if (doLog)
                                Debug.Log($"[Perception] {selfUnit.Uid}: цель убежала — теряем");

                            selfCombat.CurrentTarget = Entity.Null;
                            selfCombat.IsInCombat = false;
                            em.SetComponentData(mobEntity, selfCombat);
                        }
                        else
                        {
                            continue;   // цель валидна
                        }
                    }
                }

                // ------------------------------------------------------------
                // ФАЗА Б: ПОИСК ЦЕЛИ
                // ------------------------------------------------------------
                if (selfUnit.AggroRadius <= 0f)
                {
                    if (doLog)
                        Debug.Log($"[Perception] {selfUnit.Uid}: aggro=0 — скип");
                    continue;
                }

                float3 selfPos = selfTransform.Position;
                string selfFaction = selfUnit.Faction.ToString();
                float radiusSq = selfUnit.AggroRadius * selfUnit.AggroRadius;

                if (doLog)
                    Debug.Log($"[Perception] {selfUnit.Uid}: ищем врагов, faction={selfFaction}, radius={selfUnit.AggroRadius}");

                Entity bestTarget = Entity.Null;
                float bestDistSq = float.MaxValue;

                for (int i = 0; i < aliveEntities.Length; i++)
                {
                    var other = aliveEntities[i];
                    if (other == mobEntity) continue;

                    var otherUnit = em.GetComponentData<UnitComponent>(other);
                    string otherFaction = otherUnit.Faction.ToString();

                    bool hostile = FactionsDatabase.IsHostile(selfFaction, otherFaction);
                    bool isPlayer = em.HasComponent<PlayerTag>(other);

                    float3 otherPos = em.GetComponentData<LocalTransform>(other).Position;
                    float3 diff = otherPos - selfPos;
                    diff.y = 0f;
                    float distSq = math.lengthsq(diff);
                    float dist = math.sqrt(distSq);

                    if (doLog && isPlayer)
                    {
                        Debug.Log($"  └─ Проверка игрока E{other.Index}: " +
                                  $"faction={otherFaction}, hostile={hostile}, dist={dist:F1}, " +
                                  $"inRadius={distSq <= radiusSq}");
                    }

                    if (!hostile) continue;
                    if (distSq > radiusSq) continue;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestTarget = other;
                    }
                }

                if (bestTarget != Entity.Null)
                {
                    selfCombat.CurrentTarget = bestTarget;
                    selfCombat.IsInCombat = true;
                    em.SetComponentData(mobEntity, selfCombat);

                    Debug.Log($"👿 [Perception] Моб {selfUnit.Uid} заагрил E{bestTarget.Index} (dist={math.sqrt(bestDistSq):F1})");
                }
                else
                {
                    if (doLog)
                        Debug.Log($"[Perception] {selfUnit.Uid}: никого не нашли");
                }
            }

            aliveEntities.Dispose();
            mobEntities.Dispose();
        }
    }
}

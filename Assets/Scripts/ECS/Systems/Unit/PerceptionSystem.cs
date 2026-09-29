using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AiSystem))]
    public partial class PerceptionSystem : SystemBase
    {
        private const float LEASH_MULTIPLIER = 1.5f;

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // ================================================================
            // 1. СОБИРАЕМ ВСЕХ ЖИВЫХ ЮНИТОВ
            // ================================================================
            var aliveUnits = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            // ================================================================
            // 2. СОБИРАЕМ ВСЕХ МОБОВ
            // ================================================================
            var aiControlledUnits = em.CreateEntityQuery(
                ComponentType.ReadWrite<AiComponent>(),
                ComponentType.ReadWrite<CombatStateComponent>(),
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            foreach (var aiControlledUnit in aiControlledUnits)
            {
                var ai = em.GetComponentData<AiComponent>(aiControlledUnit);
                if (!ai.IsFromFactory) continue;

                var unit = em.GetComponentData<UnitComponent>(aiControlledUnit);
                var localTransform = em.GetComponentData<LocalTransform>(aiControlledUnit);
                var combatState = em.GetComponentData<CombatStateComponent>(aiControlledUnit);

                // ------------------------------------------------------------
                // ФАЗА А: ЕСТЬ ЦЕЛЬ
                // ------------------------------------------------------------
                if (combatState.CurrentTarget != Entity.Null)
                {
                    bool targetDead = !em.Exists(combatState.CurrentTarget)
                                      || em.HasComponent<IsDeadTag>(combatState.CurrentTarget);

                    if (targetDead)
                    {
                        combatState.CurrentTarget = Entity.Null;
                        combatState.IsInCombat = false;
                        em.SetComponentData(aiControlledUnit, combatState);
                    }
                    else
                    {
                        float3 vectorToTarget = em.GetComponentData<LocalTransform>(combatState.CurrentTarget).Position - localTransform.Position;
                        vectorToTarget.y = 0f;
                        float distanceToTarget = math.length(vectorToTarget);
                        float leashDistance = unit.AggroRadius * LEASH_MULTIPLIER;

                        if (distanceToTarget > leashDistance)
                        {
                            combatState.CurrentTarget = Entity.Null;
                            combatState.IsInCombat = false;
                            em.SetComponentData(aiControlledUnit, combatState);
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
                if (unit.AggroRadius <= 0f) continue;

                float3 currentUnitPosition = localTransform.Position;
                string currentUnitFaction = unit.Faction.ToString();
                float radiusSquared = unit.AggroRadius * unit.AggroRadius;

                Entity closestTarget = Entity.Null;
                float minDistanceSq = float.MaxValue; 

                for (int i = 0; i < aliveUnits.Length; i++)
                {
                    var otherUnitEntity = aliveUnits[i];
                    if (otherUnitEntity == aiControlledUnit) continue;

                    var otherUnit = em.GetComponentData<UnitComponent>(otherUnitEntity);
                    string otherUnitFaction = otherUnit.Faction.ToString();

                    if (!FactionsDatabase.IsHostile(currentUnitFaction, otherUnitFaction)) continue;

                    float3 otherUnitPosition = em.GetComponentData<LocalTransform>(otherUnitEntity).Position;
                    float3 differenceVector = otherUnitPosition - currentUnitPosition;
                    differenceVector.y = 0f;
                    float distanceSquared = math.lengthsq(differenceVector);

                    if (distanceSquared > radiusSquared) continue;

                    if (distanceSquared < minDistanceSq)
                    {
                        minDistanceSq = distanceSquared;
                        closestTarget = otherUnitEntity;
                    }
                }

                if (closestTarget != Entity.Null)
                {
                    combatState.CurrentTarget = closestTarget;
                    combatState.IsInCombat = true;
                    em.SetComponentData(aiControlledUnit, combatState);
                }
            }

            aliveUnits.Dispose();
            aiControlledUnits.Dispose();
        }
    }
}

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
        private const float TICK_INTERVAL = 0.1f;   // 10 раз в секунду

        private float _nextTickTime;

        protected override void OnUpdate()
        {
            // Троттлинг: не чаще 10 раз в секунду
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if (currentTime < _nextTickTime) return;
            _nextTickTime = currentTime + TICK_INTERVAL;

            var em = EntityManager;

            var aliveUnits = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            var aiControlledUnits = em.CreateEntityQuery(
                ComponentType.ReadWrite<AiComponent>(),
                ComponentType.ReadWrite<CombatStateComponent>(),
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            // Найти игрока (для вычисления Relation)
            Entity playerEntity = Entity.Null;
            FactionId playerFaction = FactionId.NeutralHumanoid;   // 🎯 было string

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<UnitComponent>()))
            {
                if (!q.IsEmpty)
                {
                    playerEntity = q.GetSingletonEntity();
                    playerFaction = em.GetComponentData<UnitComponent>(playerEntity).Faction;   // 🎯 без ToString
                }
            }

            foreach (var aiControlledUnit in aiControlledUnits)
            {
                var ai = em.GetComponentData<AiComponent>(aiControlledUnit);

                // 🆕 ИГРОК ОБРАБАТЫВАЕТСЯ ДО ПРОВЕРКИ IsFromFactory
                var combatState = em.GetComponentData<CombatStateComponent>(aiControlledUnit);

                // Игрок
                if (aiControlledUnit == playerEntity)
                {
                    combatState.Relation = NameplateRelation.Player;
                    em.SetComponentData(aiControlledUnit, combatState);
                    continue;
                }

                if (!ai.IsFromFactory) continue;

                var unit = em.GetComponentData<UnitComponent>(aiControlledUnit);
                var localTransform = em.GetComponentData<LocalTransform>(aiControlledUnit);

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
                        }
                    }
                }

                // ------------------------------------------------------------
                // ФАЗА Б: ПОИСК ЦЕЛИ (если нет текущей)
                // ------------------------------------------------------------
                if (combatState.CurrentTarget == Entity.Null && unit.AggroRadius > 0f)
                {
                    float3 currentUnitPosition = localTransform.Position;
                    FactionId currentUnitFaction = unit.Faction;   // 🎯 было string
                    float radiusSquared = unit.AggroRadius * unit.AggroRadius;

                    Entity closestTarget = Entity.Null;
                    float minDistanceSq = float.MaxValue;

                    for (int i = 0; i < aliveUnits.Length; i++)
                    {
                        var otherUnitEntity = aliveUnits[i];
                        if (otherUnitEntity == aiControlledUnit) continue;

                        var otherUnit = em.GetComponentData<UnitComponent>(otherUnitEntity);

                        // 🎯 было: string otherUnitFaction = otherUnit.Faction;
                        // теперь сравнение сразу по enum
                        if (!FactionsDatabase.IsHostile(currentUnitFaction, otherUnit.Faction)) continue;

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
                    }
                }

                // ------------------------------------------------------------
                // ФАЗА В: ВЫЧИСЛЕНИЕ RELATION (для UI)
                // ------------------------------------------------------------
                if (combatState.IsInCombat && combatState.CurrentTarget == playerEntity)
                {
                    // Атакует игрока — враждебен
                    combatState.Relation = NameplateRelation.Hostile;
                }
                else
                {
                    // По фракции
                    // 🎯 было: string unitFaction = unit.Faction;
                    // теперь сравнение сразу по unit.Faction
                    if (FactionsDatabase.IsHostile(unit.Faction, playerFaction))
                        combatState.Relation = NameplateRelation.Hostile;
                    else if (FactionsDatabase.IsFriendly(unit.Faction, playerFaction))
                        combatState.Relation = NameplateRelation.Friendly;
                    else
                        combatState.Relation = NameplateRelation.Neutral;
                }

                em.SetComponentData(aiControlledUnit, combatState);
            }

            aliveUnits.Dispose();
            aiControlledUnits.Dispose();
        }
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Abilities;

using quaternion = Unity.Mathematics.quaternion;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AiSystem : SystemBase
    {
        private Unity.Mathematics.Random _random;

        private const float SEPARATION_BUBBLE = 0.8f;
        private const float BLEND_MOVE = 0.3f;
        private const float BLEND_AVOID = 0.7f;
        private const float LEASH_MULTIPLIER = 2.5f;
        private const float ATTACK_COOLDOWN_FALLBACK = 1.5f;

        // ================================================================
        // ВЫБОР ЛУЧШЕЙ СПОСОБНОСТИ — через AbilityValidator
        // ================================================================
        private static bool TrySelectAbility(
            UnitConfig unitCfg,
            Entity caster,
            EntityManager em,
            out string selectedAbilityId,
            out float selectedAbilityRange)
        {
            selectedAbilityId = "melee_attack";
            selectedAbilityRange = 1.5f;

            if (unitCfg.abilities == null || unitCfg.abilities.Count == 0)
                return true;

            float highestScore = -999999f;
            bool foundValid = false;

            for (int i = 0; i < unitCfg.abilities.Count; i++)
            {
                string abilityId = unitCfg.abilities[i];
                var cfg = AbilitiesDatabase.GetAbility(abilityId);
                if (cfg == null) continue;

                var validation = AbilityValidator.CheckCastPossibility(abilityId, caster, em);
                if (!validation.IsPossible) continue;

                float currentScore = 1.0f;

                if (cfg.identity != null && cfg.identity.tags != null)
                {
                    for (int t = 0; t < cfg.identity.tags.Count; t++)
                    {
                        string tagName = cfg.identity.tags[t];

                        if (unitCfg.ai != null
                            && unitCfg.ai.tag_weights != null
                            && unitCfg.ai.tag_weights.TryGetValue(tagName, out float multiplier))
                        {
                            currentScore *= multiplier;
                        }
                    }
                }

                if (currentScore > highestScore)
                {
                    highestScore = currentScore;
                    selectedAbilityId = abilityId;
                    selectedAbilityRange = cfg.parameters != null ? cfg.parameters.range : 1.5f;
                    foundValid = true;
                }
            }

            return foundValid;
        }

        private float3 ApplySeparation(
            Entity currentUnitEntity, float3 currentUnitPosition, float3 moveDirection,
            string currentUnitFaction, float currentUnitHitbox,
            NativeArray<Entity> allUnits, EntityManager em)
        {
            float3 avoidDirection = float3.zero;
            int neighborsCount = 0;

            for (int i = 0; i < allUnits.Length; i++)
            {
                var otherUnitEntity = allUnits[i];
                if (otherUnitEntity == currentUnitEntity) continue;
                if (em.HasComponent<IsDeadTag>(otherUnitEntity)) continue;
                if (em.HasComponent<PlayerTag>(otherUnitEntity)) continue;

                var otherUnit = em.GetComponentData<UnitComponent>(otherUnitEntity);
                if (otherUnit.Faction.ToString() != currentUnitFaction) continue;
                if (!em.HasComponent<LocalTransform>(otherUnitEntity)) continue;
                if (!em.HasComponent<MovementComponent>(otherUnitEntity)) continue;

                float3 otherUnitPosition = em.GetComponentData<LocalTransform>(otherUnitEntity).Position;
                float otherUnitHitbox = em.GetComponentData<MovementComponent>(otherUnitEntity).HitboxRadius;

                float3 differenceVector = otherUnitPosition - currentUnitPosition;
                differenceVector.y = 0f;
                float distanceSquared = math.lengthsq(differenceVector);
                float minAllowedDistance = currentUnitHitbox + otherUnitHitbox + SEPARATION_BUBBLE;

                if (distanceSquared > 0.0001f && distanceSquared <= minAllowedDistance * minAllowedDistance)
                {
                    float3 separationVector = currentUnitPosition - otherUnitPosition;
                    separationVector.y = 0f;

                    float3 tangent = new float3(-separationVector.z, 0f, separationVector.x);
                    if (math.dot(tangent, moveDirection) < 0)
                        tangent = -tangent;

                    avoidDirection += math.normalize(tangent);
                    neighborsCount++;
                }
            }

            if (neighborsCount > 0)
            {
                avoidDirection = math.normalize(avoidDirection);
                float3 blended = moveDirection * BLEND_MOVE + avoidDirection * BLEND_AVOID;
                return math.normalize(blended);
            }

            return moveDirection;
        }

        protected override void OnCreate()
        {
            _random = new Unity.Mathematics.Random(98765);
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);

            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            _random = new Unity.Mathematics.Random((uint)(currentTime * 10000) + 1);

            var allUnits = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<MovementComponent>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            foreach (var (ai, movement, combatState, localTransform, unit, entity) in
                     SystemAPI.Query<RefRW<AiComponent>, RefRW<MovementComponent>,
                                     RefRO<CombatStateComponent>, RefRW<LocalTransform>,
                                     RefRO<UnitComponent>>()
                     .WithEntityAccess()
                     .WithNone<IsDeadTag>())
            {
                if (!ai.ValueRO.IsFromFactory) continue;

                string uIdStr = unit.ValueRO.UnitId.ToString().ToLower().Trim();
                var unitCfg = UnitsDatabase.GetUnit(uIdStr);
                if (unitCfg == null) continue;

                string faction = unit.ValueRO.Faction.ToString();
                float hitboxRadius = movement.ValueRO.HitboxRadius;

                var aiRW = ai.ValueRW;
                var moveRW = movement.ValueRW;
                var combatRO = combatState.ValueRO;

                // ============================================================
                // 🆕 ПЕРЕХОД Idle/Patrol → Chase по сигналу Perception
                // ============================================================
                if (combatRO.IsInCombat 
                    && combatRO.CurrentTarget != Entity.Null
                    && em.Exists(combatRO.CurrentTarget)
                    && (aiRW.State == AiState.Idle || aiRW.State == AiState.Patrol))
                {
                    aiRW.State = AiState.Chase;
                    aiRW.HasPatrolTarget = false;
                }

                // ============================================================
                // СБРОС: если бой закончился (Perception убрал цель)
                // ============================================================
                if (!combatRO.IsInCombat || combatRO.CurrentTarget == Entity.Null)
                {
                    if (aiRW.State == AiState.Chase || aiRW.State == AiState.Attack)
                    {
                        aiRW.State = AiState.Idle;
                        aiRW.AttackTimer = 0f;
                        aiRW.HasPatrolTarget = false;
                        moveRW.Direction = float3.zero;

                        ai.ValueRW = aiRW;
                        movement.ValueRW = moveRW;
                        continue;
                    }
                }

                // ============================================================
                // STATE: ATTACK / CHASE
                // ============================================================
                if (aiRW.State == AiState.Attack || aiRW.State == AiState.Chase)
                {
                    if (!em.Exists(combatRO.CurrentTarget) || em.HasComponent<IsDeadTag>(combatRO.CurrentTarget))
                    {
                        aiRW.State = AiState.Idle;
                        aiRW.AttackTimer = 0f;
                        moveRW.Direction = float3.zero;

                        ai.ValueRW = aiRW;
                        movement.ValueRW = moveRW;
                        continue;
                    }

                    float3 toTarget = em.GetComponentData<LocalTransform>(combatRO.CurrentTarget).Position - localTransform.ValueRO.Position;
                    toTarget.y = 0f;
                    float distanceToTarget = math.length(toTarget);

                    // ============================================================
                    // 🦾 ММО-ПОВОРОТ К ЦЕЛИ (Исправили сквозной адрес метода LookRotation)
                    // ============================================================
                    if (math.lengthsq(toTarget) > 0.001f)
                    {
                        float3 lookDirection = math.normalize(toTarget);
                        
                        // 🦾 СИ-ФИКС: Полный неубиваемый путь к методу LookRotation
                        quaternion targetRotation = quaternion.LookRotation(lookDirection, new float3(0f, 1f, 0f));

                        // Сферическая интерполяция (slerp) для шёлковой плавности
                        quaternion blendedRotation = math.slerp(
                            localTransform.ValueRO.Rotation, 
                            targetRotation, 
                            12f * deltaTime
                        );

                        var transformRW = localTransform.ValueRW;
                        transformRW.Rotation = blendedRotation;
                        localTransform.ValueRW = transformRW;
                    }


                    float leashDistance = unit.ValueRO.AggroRadius * LEASH_MULTIPLIER;
                    if (distanceToTarget > leashDistance)
                    {
                        aiRW.State = AiState.Idle;
                        aiRW.AttackTimer = 0f;
                        moveRW.Direction = float3.zero;

                        ai.ValueRW = aiRW;
                        movement.ValueRW = moveRW;
                        continue;
                    }

                    bool isCasting = em.HasComponent<CastComponent>(entity)
                                     && em.GetComponentData<CastComponent>(entity).IsActive;
                    if (isCasting)
                    {
                        moveRW.Direction = float3.zero;
                        
                        ai.ValueRW = aiRW; // Обязательно сохраняем стейт ИИ
                        movement.ValueRW = moveRW;
                        continue;
                    }

                    if (aiRW.AttackTimer > 0f)
                    {
                        aiRW.AttackTimer -= deltaTime;
                        moveRW.Direction = float3.zero;

                        ai.ValueRW = aiRW;
                        movement.ValueRW = moveRW;
                        continue;
                    }

                    bool hasAbility = TrySelectAbility(
                        unitCfg, entity, em,
                        out string selectedAbilityId, out float selectedAbilityRange);

                    if (hasAbility)
                    {
                        aiRW.State = AiState.Attack;
                        aiRW.AttackRange = selectedAbilityRange;
                        aiRW.PrimaryAbility = selectedAbilityId;
                        aiRW.AttackTimer = ATTACK_COOLDOWN_FALLBACK;
                        moveRW.Direction = float3.zero;

                        Entity requestEntity = ecb.CreateEntity();
                        ecb.AddComponent(requestEntity, new CastRequest
                        {
                            Caster = entity,
                            AbilityId = selectedAbilityId,
                            TargetEntity = combatRO.CurrentTarget,
                        });
                    }
                    else
                    {
                        aiRW.State = AiState.Chase;

                        float3 direction = math.normalize(toTarget);
                        direction = ApplySeparation(entity, localTransform.ValueRO.Position, direction, faction, hitboxRadius, allUnits, em);

                        moveRW.Direction = direction;
                        moveRW.CurrentSpeed = unitCfg.parameters.base_speed;
                    }

                    ai.ValueRW = aiRW;
                    movement.ValueRW = moveRW;
                    continue;
                }

                // ============================================================
                // STATE: IDLE / PATROL
                // ============================================================
                if (aiRW.State == AiState.Idle && currentTime >= aiRW.NextActionTime)
                {
                    float2 offset = _random.NextFloat2Direction() * _random.NextFloat(2f, aiRW.PatrolRadius);
                    aiRW.PatrolPoint = aiRW.StartPoint + new float3(offset.x, 0f, offset.y);
                    aiRW.HasPatrolTarget = true;
                    
                    // 🦾 СИ-ФИКС: Больше не взводим грязный булеан, стейт говорит сам за себя!
                    aiRW.State = AiState.Patrol;
                }

                if (aiRW.State == AiState.Patrol && aiRW.HasPatrolTarget)
                {
                    float3 vectorToTarget = aiRW.PatrolPoint - localTransform.ValueRO.Position;
                    vectorToTarget.y = 0f;
                    float distanceToPatrolPoint = math.length(vectorToTarget);

                    if (distanceToPatrolPoint < 0.4f)
                    {
                        moveRW.Direction = float3.zero;
                        aiRW.HasPatrolTarget = false;
                        aiRW.State = AiState.Idle;

                        float randomDelay = _random.NextInt(10, 31) / 10f;
                        aiRW.NextActionTime = currentTime + randomDelay;

                        ai.ValueRW = aiRW;
                        movement.ValueRW = moveRW;
                        continue;
                    }

                    float3 movementDirection = math.normalize(vectorToTarget);
                    movementDirection = ApplySeparation(entity, localTransform.ValueRO.Position, movementDirection, faction, hitboxRadius, allUnits, em);

                    moveRW.Direction = movementDirection;

                    float workingSpeed = unitCfg.parameters.base_speed;
                    
                    // 🦾 СИ-ФИКС: Проверяем семантически чистый энум стейта вместо булевых костылей!
                    if (aiRW.State == AiState.Patrol)
                    {
                        workingSpeed *= 0.5f;
                    }

                    moveRW.CurrentSpeed = workingSpeed;
                }

                ai.ValueRW = aiRW;
                movement.ValueRW = moveRW;
            }

            allUnits.Dispose();

        }
    }
}

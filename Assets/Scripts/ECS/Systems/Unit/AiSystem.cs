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
        private const float PATROL_SPEED_MULTIPLIER = 0.5f;
        private const float PATROL_REACHED_DISTANCE = 0.4f;
        private const float ROTATION_SPEED = 12f;
        private const float PATROL_MIN_DELAY = 1.0f;
        private const float PATROL_MAX_DELAY = 3.0f;

        // ================================================================
        // КОНТЕКСТ ЮНИТА — всё, что нужно методам стейтов
        // ================================================================
        private struct AiContext
        {
            public EntityManager Em;
            public EntityCommandBuffer Ecb;
            public Entity Entity;

            public AiComponent Ai;
            public MovementComponent Move;
            public LocalTransform Transform;
            public CombatStateComponent Combat;
            public UnitComponent Unit;
            public UnitConfig UnitCfg;

            public NativeArray<Entity> AllUnits;
            public float CurrentTime;
            public float DeltaTime;
            public Unity.Mathematics.Random Random;

            public string Faction => Unit.Faction.ToString();
            public float3 Position => Transform.Position;
            public float HitboxRadius => Move.HitboxRadius;
            public bool HasTarget => Combat.IsInCombat && Combat.CurrentTarget != Entity.Null;
        }

        // ================================================================
        // ЖИЗНЕННЫЙ ЦИКЛ
        // ================================================================
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

                // 🦾 СВЕРХЗВУКОВОЙ ГВАРД НЕЙТРАЛИТЕТА: 
                // Если бэкенд выставил мирный/нейтральный паспорт — юнит мгновенно
                // останавливается, сбрасывает скорость в ноль и скипает ИИ-логику кадра!
                if (ai.ValueRO.Profile == AiProfileKind.NeutralPassive || ai.ValueRO.Profile == AiProfileKind.Passive)
                {
                    ai.ValueRW.State = AiState.Idle;
                    movement.ValueRW.Direction = float3.zero;
                    continue;
                }

                string uIdStr = unit.ValueRO.UnitId.ToString().ToLower().Trim();
                var unitCfg = UnitsDatabase.GetUnit(uIdStr);
                if (unitCfg == null) continue;

                var ctx = new AiContext
                {
                    Em = em,
                    Ecb = ecb,
                    Entity = entity,
                    Ai = ai.ValueRO,
                    Move = movement.ValueRO,
                    Transform = localTransform.ValueRO,
                    Combat = combatState.ValueRO,
                    Unit = unit.ValueRO,
                    UnitCfg = unitCfg,
                    AllUnits = allUnits,
                    CurrentTime = currentTime,
                    DeltaTime = deltaTime,
                    Random = _random,
                };

                // Роутинг по стейту
                switch (ctx.Ai.State)
                {
                    case AiState.Idle:
                        ExecuteIdleState(ref ctx);
                        break;

                    case AiState.Patrol:
                        ExecutePatrolState(ref ctx);
                        break;

                    case AiState.Chase:
                    case AiState.Attack:
                        ExecuteCombatState(ref ctx);
                        break;
                }

                // Вернуть изменения
                ai.ValueRW = ctx.Ai;
                movement.ValueRW = ctx.Move;
                localTransform.ValueRW = ctx.Transform;
                _random = ctx.Random;
            }

            allUnits.Dispose();
        }

        // ================================================================
        // СТЕЙТ: IDLE
        // ================================================================
        private void ExecuteIdleState(ref AiContext ctx)
        {
            // Perception обнаружил цель — в бой
            if (ctx.HasTarget && ctx.Em.Exists(ctx.Combat.CurrentTarget))
            {
                ctx.Ai.State = AiState.Chase;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Move.Direction = float3.zero;
                return;
            }

            // Ещё рано — стоим
            if (ctx.CurrentTime < ctx.Ai.NextActionTime)
                return;

            // Выбираем новую patrol-точку
            float2 offset = ctx.Random.NextFloat2Direction() * ctx.Random.NextFloat(2f, ctx.Ai.PatrolRadius);
            ctx.Ai.PatrolPoint = ctx.Ai.StartPoint + new float3(offset.x, 0f, offset.y);
            ctx.Ai.HasPatrolTarget = true;
            ctx.Ai.State = AiState.Patrol;
        }

        // ================================================================
        // СТЕЙТ: PATROL
        // ================================================================
        private void ExecutePatrolState(ref AiContext ctx)
        {
            // Perception обнаружил цель — в бой
            if (ctx.HasTarget && ctx.Em.Exists(ctx.Combat.CurrentTarget))
            {
                ctx.Ai.State = AiState.Chase;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Move.Direction = float3.zero;
                return;
            }

            float3 toPoint = ctx.Ai.PatrolPoint - ctx.Position;
            toPoint.y = 0f;

            // Дошли до точки — Idle с паузой
            if (math.length(toPoint) < PATROL_REACHED_DISTANCE)
            {
                ctx.Move.Direction = float3.zero;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Ai.State = AiState.Idle;

                float delay = ctx.Random.NextFloat(PATROL_MIN_DELAY, PATROL_MAX_DELAY);
                ctx.Ai.NextActionTime = ctx.CurrentTime + delay;
                return;
            }

            float3 direction = math.normalize(toPoint);
            direction = ApplySeparation(ref ctx, direction);

            ctx.Move.Direction = direction;
            ctx.Move.CurrentSpeed = ctx.UnitCfg.parameters.base_speed * PATROL_SPEED_MULTIPLIER;
        }

        // ================================================================
        // СТЕЙТ: CHASE / ATTACK (объединены в один поток)
        // ================================================================
        private void ExecuteCombatState(ref AiContext ctx)
        {
            // Валидация цели
            if (!ctx.Em.Exists(ctx.Combat.CurrentTarget) ||
                ctx.Em.HasComponent<IsDeadTag>(ctx.Combat.CurrentTarget))
            {
                ExitCombat(ref ctx);
                return;
            }

            float3 toTarget = ctx.Em.GetComponentData<LocalTransform>(ctx.Combat.CurrentTarget).Position - ctx.Position;
            toTarget.y = 0f;
            float distanceToTarget = math.length(toTarget);

            // Поворот к цели
            FaceTarget(ref ctx, toTarget);

            // Leash — цель слишком далеко
            float leashDistance = ctx.Unit.AggroRadius * LEASH_MULTIPLIER;
            if (distanceToTarget > leashDistance)
            {
                ExitCombat(ref ctx);
                return;
            }

            // Каст уже идёт — стоим
            if (IsCasting(ctx.Entity, ctx.Em))
            {
                ctx.Move.Direction = float3.zero;
                return;
            }

            // Кулдаун атаки — стоим
            if (ctx.Ai.AttackTimer > 0f)
            {
                ctx.Ai.AttackTimer -= ctx.DeltaTime;
                ctx.Move.Direction = float3.zero;
                return;
            }

            // Выбор и запуск способности
            bool hasAbility = TrySelectAbility(
                ctx.UnitCfg, ctx.Entity, ctx.Em,
                out string selectedAbilityId, out float selectedAbilityRange);

            if (hasAbility)
            {
                ctx.Ai.State = AiState.Attack;
                ctx.Ai.AttackRange = selectedAbilityRange;
                ctx.Ai.PrimaryAbility = selectedAbilityId;
                ctx.Ai.AttackTimer = ATTACK_COOLDOWN_FALLBACK;
                ctx.Move.Direction = float3.zero;

                Entity requestEntity = ctx.Ecb.CreateEntity();
                ctx.Ecb.AddComponent(requestEntity, new CastRequest
                {
                    Caster = ctx.Entity,
                    AbilityId = selectedAbilityId,
                    TargetEntity = ctx.Combat.CurrentTarget,
                });
            }
            else
            {
                ctx.Ai.State = AiState.Chase;

                float3 direction = math.normalize(toTarget);
                direction = ApplySeparation(ref ctx, direction);

                ctx.Move.Direction = direction;
                ctx.Move.CurrentSpeed = ctx.UnitCfg.parameters.base_speed;
            }
        }

        private void ExitCombat(ref AiContext ctx)
        {
            ctx.Ai.State = AiState.Idle;
            ctx.Ai.AttackTimer = 0f;
            ctx.Ai.HasPatrolTarget = false;
            ctx.Move.Direction = float3.zero;
        }

        // ================================================================
        // ХЕЛПЕРЫ
        // ================================================================
        private void FaceTarget(ref AiContext ctx, float3 toTarget)
        {
            if (math.lengthsq(toTarget) < 0.001f) return;

            float3 lookDir = math.normalize(toTarget);
            quaternion targetRot = quaternion.LookRotation(lookDir, new float3(0f, 1f, 0f));

            ctx.Transform.Rotation = math.slerp(
                ctx.Transform.Rotation,
                targetRot,
                ROTATION_SPEED * ctx.DeltaTime);
        }

        private static bool IsCasting(Entity entity, EntityManager em)
        {
            return em.HasComponent<CastComponent>(entity)
                && em.GetComponentData<CastComponent>(entity).IsActive;
        }

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

        // ================================================================
        // SEPARATION — расталкивание соседей
        // ================================================================
        private float3 ApplySeparation(ref AiContext ctx, float3 moveDirection)
        {
            float3 avoidDirection = float3.zero;
            int neighborsCount = 0;

            for (int i = 0; i < ctx.AllUnits.Length; i++)
            {
                var otherEntity = ctx.AllUnits[i];
                if (otherEntity == ctx.Entity) continue;
                if (ctx.Em.HasComponent<IsDeadTag>(otherEntity)) continue;
                if (ctx.Em.HasComponent<PlayerTag>(otherEntity)) continue;

                var otherUnit = ctx.Em.GetComponentData<UnitComponent>(otherEntity);
                if (otherUnit.Faction.ToString() != ctx.Faction) continue;
                if (!ctx.Em.HasComponent<LocalTransform>(otherEntity)) continue;
                if (!ctx.Em.HasComponent<MovementComponent>(otherEntity)) continue;

                float3 otherPos = ctx.Em.GetComponentData<LocalTransform>(otherEntity).Position;
                float otherHitbox = ctx.Em.GetComponentData<MovementComponent>(otherEntity).HitboxRadius;

                float3 diff = otherPos - ctx.Position;
                diff.y = 0f;
                float distSq = math.lengthsq(diff);
                float minAllowed = ctx.HitboxRadius + otherHitbox + SEPARATION_BUBBLE;

                if (distSq > 0.0001f && distSq <= minAllowed * minAllowed)
                {
                    float3 sep = ctx.Position - otherPos;
                    sep.y = 0f;

                    float3 tangent = new float3(-sep.z, 0f, sep.x);
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
    }
}

// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
// using ProjectTowerRpg.ECS.Components;
// using ProjectTowerRpg.Core.Units;
// using ProjectTowerRpg.Core.Abilities;
//
// using quaternion = Unity.Mathematics.quaternion;
//
// namespace ProjectTowerRpg.ECS.Systems
// {
//     [UpdateInGroup(typeof(SimulationSystemGroup))]
//     public partial class AiSystem : SystemBase
//     {
//         private Unity.Mathematics.Random _random;
//
//         private const float SEPARATION_BUBBLE = 0.8f;
//         private const float BLEND_MOVE = 0.3f;
//         private const float BLEND_AVOID = 0.7f;
//         private const float LEASH_MULTIPLIER = 2.5f;
//         private const float ATTACK_COOLDOWN_FALLBACK = 1.5f;
//
//         // ================================================================
//         // ВЫБОР ЛУЧШЕЙ СПОСОБНОСТИ — через AbilityValidator
//         // ================================================================
//         private static bool TrySelectAbility(
//             UnitConfig unitCfg,
//             Entity caster,
//             EntityManager em,
//             out string selectedAbilityId,
//             out float selectedAbilityRange)
//         {
//             selectedAbilityId = "melee_attack";
//             selectedAbilityRange = 1.5f;
//
//             if (unitCfg.abilities == null || unitCfg.abilities.Count == 0)
//                 return true;
//
//             float highestScore = -999999f;
//             bool foundValid = false;
//
//             for (int i = 0; i < unitCfg.abilities.Count; i++)
//             {
//                 string abilityId = unitCfg.abilities[i];
//                 var cfg = AbilitiesDatabase.GetAbility(abilityId);
//                 if (cfg == null) continue;
//
//                 var validation = AbilityValidator.CheckCastPossibility(abilityId, caster, em);
//                 if (!validation.IsPossible) continue;
//
//                 float currentScore = 1.0f;
//
//                 if (cfg.identity != null && cfg.identity.tags != null)
//                 {
//                     for (int t = 0; t < cfg.identity.tags.Count; t++)
//                     {
//                         string tagName = cfg.identity.tags[t];
//
//                         if (unitCfg.ai != null
//                             && unitCfg.ai.tag_weights != null
//                             && unitCfg.ai.tag_weights.TryGetValue(tagName, out float multiplier))
//                         {
//                             currentScore *= multiplier;
//                         }
//                     }
//                 }
//
//                 if (currentScore > highestScore)
//                 {
//                     highestScore = currentScore;
//                     selectedAbilityId = abilityId;
//                     selectedAbilityRange = cfg.parameters != null ? cfg.parameters.range : 1.5f;
//                     foundValid = true;
//                 }
//             }
//
//             return foundValid;
//         }
//
//         private float3 ApplySeparation(
//             Entity currentUnitEntity, float3 currentUnitPosition, float3 moveDirection,
//             string currentUnitFaction, float currentUnitHitbox,
//             NativeArray<Entity> allUnits, EntityManager em)
//         {
//             float3 avoidDirection = float3.zero;
//             int neighborsCount = 0;
//
//             for (int i = 0; i < allUnits.Length; i++)
//             {
//                 var otherUnitEntity = allUnits[i];
//                 if (otherUnitEntity == currentUnitEntity) continue;
//                 if (em.HasComponent<IsDeadTag>(otherUnitEntity)) continue;
//                 if (em.HasComponent<PlayerTag>(otherUnitEntity)) continue;
//
//                 var otherUnit = em.GetComponentData<UnitComponent>(otherUnitEntity);
//                 if (otherUnit.Faction.ToString() != currentUnitFaction) continue;
//                 if (!em.HasComponent<LocalTransform>(otherUnitEntity)) continue;
//                 if (!em.HasComponent<MovementComponent>(otherUnitEntity)) continue;
//
//                 float3 otherUnitPosition = em.GetComponentData<LocalTransform>(otherUnitEntity).Position;
//                 float otherUnitHitbox = em.GetComponentData<MovementComponent>(otherUnitEntity).HitboxRadius;
//
//                 float3 differenceVector = otherUnitPosition - currentUnitPosition;
//                 differenceVector.y = 0f;
//                 float distanceSquared = math.lengthsq(differenceVector);
//                 float minAllowedDistance = currentUnitHitbox + otherUnitHitbox + SEPARATION_BUBBLE;
//
//                 if (distanceSquared > 0.0001f && distanceSquared <= minAllowedDistance * minAllowedDistance)
//                 {
//                     float3 separationVector = currentUnitPosition - otherUnitPosition;
//                     separationVector.y = 0f;
//
//                     float3 tangent = new float3(-separationVector.z, 0f, separationVector.x);
//                     if (math.dot(tangent, moveDirection) < 0)
//                         tangent = -tangent;
//
//                     avoidDirection += math.normalize(tangent);
//                     neighborsCount++;
//                 }
//             }
//
//             if (neighborsCount > 0)
//             {
//                 avoidDirection = math.normalize(avoidDirection);
//                 float3 blended = moveDirection * BLEND_MOVE + avoidDirection * BLEND_AVOID;
//                 return math.normalize(blended);
//             }
//
//             return moveDirection;
//         }
//
//         protected override void OnCreate()
//         {
//             _random = new Unity.Mathematics.Random(98765);
//         }
//
//         protected override void OnUpdate()
//         {
//             var em = EntityManager;
//             var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
//                 .CreateCommandBuffer(World.Unmanaged);
//
//             float currentTime = (float)SystemAPI.Time.ElapsedTime;
//             float deltaTime = SystemAPI.Time.DeltaTime;
//
//             _random = new Unity.Mathematics.Random((uint)(currentTime * 10000) + 1);
//
//             var allUnits = em.CreateEntityQuery(
//                 ComponentType.ReadOnly<UnitComponent>(),
//                 ComponentType.ReadOnly<LocalTransform>(),
//                 ComponentType.ReadOnly<MovementComponent>(),
//                 ComponentType.Exclude<IsDeadTag>()
//             ).ToEntityArray(Allocator.Temp);
//
//             foreach (var (ai, movement, combatState, localTransform, unit, entity) in
//                      SystemAPI.Query<RefRW<AiComponent>, RefRW<MovementComponent>,
//                                      RefRO<CombatStateComponent>, RefRW<LocalTransform>,
//                                      RefRO<UnitComponent>>()
//                      .WithEntityAccess()
//                      .WithNone<IsDeadTag>())
//             {
//                 if (!ai.ValueRO.IsFromFactory) continue;
//
//                 string uIdStr = unit.ValueRO.UnitId.ToString().ToLower().Trim();
//                 var unitCfg = UnitsDatabase.GetUnit(uIdStr);
//                 if (unitCfg == null) continue;
//
//                 string faction = unit.ValueRO.Faction.ToString();
//                 float hitboxRadius = movement.ValueRO.HitboxRadius;
//
//                 var aiRW = ai.ValueRW;
//                 var moveRW = movement.ValueRW;
//                 var combatRO = combatState.ValueRO;
//
//                 // ============================================================
//                 // 🆕 ПЕРЕХОД Idle/Patrol → Chase по сигналу Perception
//                 // ============================================================
//                 if (combatRO.IsInCombat 
//                     && combatRO.CurrentTarget != Entity.Null
//                     && em.Exists(combatRO.CurrentTarget)
//                     && (aiRW.State == AiState.Idle || aiRW.State == AiState.Patrol))
//                 {
//                     aiRW.State = AiState.Chase;
//                     aiRW.HasPatrolTarget = false;
//                 }
//
//                 // ============================================================
//                 // СБРОС: если бой закончился (Perception убрал цель)
//                 // ============================================================
//                 if (!combatRO.IsInCombat || combatRO.CurrentTarget == Entity.Null)
//                 {
//                     if (aiRW.State == AiState.Chase || aiRW.State == AiState.Attack)
//                     {
//                         aiRW.State = AiState.Idle;
//                         aiRW.AttackTimer = 0f;
//                         aiRW.HasPatrolTarget = false;
//                         moveRW.Direction = float3.zero;
//
//                         ai.ValueRW = aiRW;
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//                 }
//
//                 // ============================================================
//                 // STATE: ATTACK / CHASE
//                 // ============================================================
//                 if (aiRW.State == AiState.Attack || aiRW.State == AiState.Chase)
//                 {
//                     if (!em.Exists(combatRO.CurrentTarget) || em.HasComponent<IsDeadTag>(combatRO.CurrentTarget))
//                     {
//                         aiRW.State = AiState.Idle;
//                         aiRW.AttackTimer = 0f;
//                         moveRW.Direction = float3.zero;
//
//                         ai.ValueRW = aiRW;
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//
//                     float3 toTarget = em.GetComponentData<LocalTransform>(combatRO.CurrentTarget).Position - localTransform.ValueRO.Position;
//                     toTarget.y = 0f;
//                     float distanceToTarget = math.length(toTarget);
//
//                     // ============================================================
//                     // 🦾 ММО-ПОВОРОТ К ЦЕЛИ (Исправили сквозной адрес метода LookRotation)
//                     // ============================================================
//                     if (math.lengthsq(toTarget) > 0.001f)
//                     {
//                         float3 lookDirection = math.normalize(toTarget);
//                         
//                         // 🦾 СИ-ФИКС: Полный неубиваемый путь к методу LookRotation
//                         quaternion targetRotation = quaternion.LookRotation(lookDirection, new float3(0f, 1f, 0f));
//
//                         // Сферическая интерполяция (slerp) для шёлковой плавности
//                         quaternion blendedRotation = math.slerp(
//                             localTransform.ValueRO.Rotation, 
//                             targetRotation, 
//                             12f * deltaTime
//                         );
//
//                         var transformRW = localTransform.ValueRW;
//                         transformRW.Rotation = blendedRotation;
//                         localTransform.ValueRW = transformRW;
//                     }
//
//
//                     float leashDistance = unit.ValueRO.AggroRadius * LEASH_MULTIPLIER;
//                     if (distanceToTarget > leashDistance)
//                     {
//                         aiRW.State = AiState.Idle;
//                         aiRW.AttackTimer = 0f;
//                         moveRW.Direction = float3.zero;
//
//                         ai.ValueRW = aiRW;
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//
//                     bool isCasting = em.HasComponent<CastComponent>(entity)
//                                      && em.GetComponentData<CastComponent>(entity).IsActive;
//                     if (isCasting)
//                     {
//                         moveRW.Direction = float3.zero;
//                         
//                         ai.ValueRW = aiRW; // Обязательно сохраняем стейт ИИ
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//
//                     if (aiRW.AttackTimer > 0f)
//                     {
//                         aiRW.AttackTimer -= deltaTime;
//                         moveRW.Direction = float3.zero;
//
//                         ai.ValueRW = aiRW;
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//
//                     bool hasAbility = TrySelectAbility(
//                         unitCfg, entity, em,
//                         out string selectedAbilityId, out float selectedAbilityRange);
//
//                     if (hasAbility)
//                     {
//                         aiRW.State = AiState.Attack;
//                         aiRW.AttackRange = selectedAbilityRange;
//                         aiRW.PrimaryAbility = selectedAbilityId;
//                         aiRW.AttackTimer = ATTACK_COOLDOWN_FALLBACK;
//                         moveRW.Direction = float3.zero;
//
//                         Entity requestEntity = ecb.CreateEntity();
//                         ecb.AddComponent(requestEntity, new CastRequest
//                         {
//                             Caster = entity,
//                             AbilityId = selectedAbilityId,
//                             TargetEntity = combatRO.CurrentTarget,
//                         });
//                     }
//                     else
//                     {
//                         aiRW.State = AiState.Chase;
//
//                         float3 direction = math.normalize(toTarget);
//                         direction = ApplySeparation(entity, localTransform.ValueRO.Position, direction, faction, hitboxRadius, allUnits, em);
//
//                         moveRW.Direction = direction;
//                         moveRW.CurrentSpeed = unitCfg.parameters.base_speed;
//                     }
//
//                     ai.ValueRW = aiRW;
//                     movement.ValueRW = moveRW;
//                     continue;
//                 }
//
//                 // ============================================================
//                 // STATE: IDLE / PATROL
//                 // ============================================================
//                 if (aiRW.State == AiState.Idle && currentTime >= aiRW.NextActionTime)
//                 {
//                     float2 offset = _random.NextFloat2Direction() * _random.NextFloat(2f, aiRW.PatrolRadius);
//                     aiRW.PatrolPoint = aiRW.StartPoint + new float3(offset.x, 0f, offset.y);
//                     aiRW.HasPatrolTarget = true;
//                     
//                     // 🦾 СИ-ФИКС: Больше не взводим грязный булеан, стейт говорит сам за себя!
//                     aiRW.State = AiState.Patrol;
//                 }
//
//                 if (aiRW.State == AiState.Patrol && aiRW.HasPatrolTarget)
//                 {
//                     float3 vectorToTarget = aiRW.PatrolPoint - localTransform.ValueRO.Position;
//                     vectorToTarget.y = 0f;
//                     float distanceToPatrolPoint = math.length(vectorToTarget);
//
//                     if (distanceToPatrolPoint < 0.4f)
//                     {
//                         moveRW.Direction = float3.zero;
//                         aiRW.HasPatrolTarget = false;
//                         aiRW.State = AiState.Idle;
//
//                         float randomDelay = _random.NextInt(10, 31) / 10f;
//                         aiRW.NextActionTime = currentTime + randomDelay;
//
//                         ai.ValueRW = aiRW;
//                         movement.ValueRW = moveRW;
//                         continue;
//                     }
//
//                     float3 movementDirection = math.normalize(vectorToTarget);
//                     movementDirection = ApplySeparation(entity, localTransform.ValueRO.Position, movementDirection, faction, hitboxRadius, allUnits, em);
//
//                     moveRW.Direction = movementDirection;
//
//                     float workingSpeed = unitCfg.parameters.base_speed;
//                     
//                     // 🦾 СИ-ФИКС: Проверяем семантически чистый энум стейта вместо булевых костылей!
//                     if (aiRW.State == AiState.Patrol)
//                     {
//                         workingSpeed *= 0.5f;
//                     }
//
//                     moveRW.CurrentSpeed = workingSpeed;
//                 }
//
//                 ai.ValueRW = aiRW;
//                 movement.ValueRW = moveRW;
//             }
//
//             allUnits.Dispose();
//
//         }
//     }
// }

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

        private const float SEPARATION_BUBBLE = 0.2f;
        private const float BLEND_MOVE = 0.8f;
        private const float BLEND_AVOID = 0.2f;
        private const float LEASH_MULTIPLIER = 2.5f;
        private const float ATTACK_COOLDOWN_FALLBACK = 1.5f;
        private const float PATROL_SPEED_MULTIPLIER = 0.5f;
        private const float PATROL_REACHED_DISTANCE = 0.4f;
        private const float ROTATION_SPEED = 12f;
        private const float PATROL_MIN_DELAY = 1.0f;
        private const float PATROL_MAX_DELAY = 3.0f;
        
        private const float DIR_SMOOTHING = 0.2f;
        private const float SEPARATION_MIN_STRENGTH = 0.1f;
        private const float DIR_ANGLE_THRESHOLD = 0.96f;

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

                // 🆕 АВТОМАТИЧЕСКИЙ ВЗЛЁТ ДЛЯ ПТИЦ:
                // Если юнит умеет летать и хочет двигаться (патруль или погоня) — включаем полёт.
                // Если стоит на месте (Idle) — сажаем на землю.
                if (ctx.Move.CanFly)
                {
                    if (ctx.Ai.State == AiState.Patrol || ctx.Ai.State == AiState.Chase)
                    {
                        ctx.Move.IsFlying = true;
                    }
                    else if (ctx.Ai.State == AiState.Idle || ctx.Ai.State == AiState.Attack)
                    {
                        // В бою тоже можно летать, но для простоты на земле
                        ctx.Move.IsFlying = false; 
                    }
                }

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

                ai.ValueRW = ctx.Ai;
                movement.ValueRW = ctx.Move;
                localTransform.ValueRW = ctx.Transform;
                _random = ctx.Random;
            }

            allUnits.Dispose();
        }

        private void ExecuteIdleState(ref AiContext ctx)
        {
            if (ctx.HasTarget && ctx.Em.Exists(ctx.Combat.CurrentTarget))
            {
                ctx.Ai.State = AiState.Chase;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Move.Direction = float3.zero;
                return;
            }

            if (ctx.CurrentTime < ctx.Ai.NextActionTime)
                return;

            float2 offset = ctx.Random.NextFloat2Direction() * ctx.Random.NextFloat(2f, ctx.Ai.PatrolRadius);
            // 🆕 Если летаем, точка патруля должна быть на высоте полёта
            float targetY = ctx.Move.CanFly ? ctx.Move.FlyHeight : ctx.Position.y;
            ctx.Ai.PatrolPoint = new float3(ctx.Ai.StartPoint.x + offset.x, targetY, ctx.Ai.StartPoint.z + offset.y);
            
            ctx.Ai.HasPatrolTarget = true;
            ctx.Ai.State = AiState.Patrol;
        }

        private void ExecutePatrolState(ref AiContext ctx)
        {
            if (ctx.HasTarget && ctx.Em.Exists(ctx.Combat.CurrentTarget))
            {
                ctx.Ai.State = AiState.Chase;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Move.Direction = float3.zero;
                return;
            }

            // 🆕 Если летаем, то учитываем высоту (Y)
            float3 toPoint = ctx.Ai.PatrolPoint - ctx.Position;
            if (!ctx.Move.IsFlying) 
            {
                toPoint.y = 0f; // На земле идём только по плоскости
            }

            // Проверка достижения цели. Для летающих проверяем в 3D, для пеших — в 2D
            float distanceSq = ctx.Move.IsFlying ? math.lengthsq(toPoint) : math.lengthsq(toPoint.xz);
            if (distanceSq < PATROL_REACHED_DISTANCE * PATROL_REACHED_DISTANCE)
            {
                ctx.Move.Direction = float3.zero;
                ctx.Ai.HasPatrolTarget = false;
                ctx.Ai.State = AiState.Idle;

                float delay = ctx.Random.NextFloat(PATROL_MIN_DELAY, PATROL_MAX_DELAY);
                ctx.Ai.NextActionTime = ctx.CurrentTime + delay;
                return;
            }

            float3 direction = math.normalize(toPoint);
            
            // 🆕 Разделение (Separation) для летающих лучше считать тоже в 3D, 
            // но для простоты оставим как есть, чтобы вороны не расталкивали друг друга по вертикали
            direction = ApplySeparation(ref ctx, direction);
            
            // Сглаживание направления. Для летающих лучше делать более плавные повороты
            direction = SmoothDirection(ctx.Move.Direction, direction);

            ctx.Move.Direction = direction;
            ctx.Move.CurrentSpeed = ctx.UnitCfg.parameters.base_speed * PATROL_SPEED_MULTIPLIER;

            // 🆕 Поворот в сторону полёта
            RotateTowards(ref ctx, direction);
        }

        private void ExecuteCombatState(ref AiContext ctx)
        {
            if (!ctx.Em.Exists(ctx.Combat.CurrentTarget) ||
                ctx.Em.HasComponent<IsDeadTag>(ctx.Combat.CurrentTarget))
            {
                ExitCombat(ref ctx);
                return;
            }

            float3 toTarget = ctx.Em.GetComponentData<LocalTransform>(ctx.Combat.CurrentTarget).Position - ctx.Position;
            // 🆕 Если летаем, учитываем высоту цели
            if (!ctx.Move.IsFlying) toTarget.y = 0f;
            
            float distanceToTarget = math.length(toTarget);

            float leashDistance = ctx.Unit.AggroRadius * LEASH_MULTIPLIER;
            if (distanceToTarget > leashDistance)
            {
                ExitCombat(ref ctx);
                return;
            }

            if (IsCasting(ctx.Entity, ctx.Em))
            {
                ctx.Move.Direction = float3.zero;
                return;
            }

            if (ctx.Ai.AttackTimer > 0f)
            {
                ctx.Ai.AttackTimer -= ctx.DeltaTime;
                ctx.Move.Direction = float3.zero;
                return;
            }

            bool hasAbility = TrySelectAbility(
                ctx.UnitCfg, ctx.Entity, ctx.Em,
                out string selectedAbilityId, out float selectedAbilityRange);

            float targetRadius = ctx.Em.HasComponent<MovementComponent>(ctx.Combat.CurrentTarget)
                ? ctx.Em.GetComponentData<MovementComponent>(ctx.Combat.CurrentTarget).HitboxRadius
                : 0.5f;

            float edgeDistance = distanceToTarget - ctx.HitboxRadius - targetRadius;

            if (hasAbility && edgeDistance <= selectedAbilityRange)
            {
                ctx.Ai.State = AiState.Attack;
                ctx.Ai.AttackRange = selectedAbilityRange;
                ctx.Ai.PrimaryAbility = selectedAbilityId;
                ctx.Ai.AttackTimer = ATTACK_COOLDOWN_FALLBACK;
                ctx.Move.Direction = float3.zero;

                // 🆕 Чтобы атаковать с воздуха, нужно лицом к цели
                FaceTarget(ref ctx, toTarget);

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
                direction = SmoothDirection(ctx.Move.Direction, direction);

                ctx.Move.Direction = direction;
                ctx.Move.CurrentSpeed = ctx.UnitCfg.parameters.base_speed;

                RotateTowards(ref ctx, direction);
            }
        }

        private void ExitCombat(ref AiContext ctx)
        {
            ctx.Ai.State = AiState.Idle;
            ctx.Ai.AttackTimer = 0f;
            ctx.Ai.HasPatrolTarget = false;
            ctx.Move.Direction = float3.zero;
            // 🆕 При выходе из боя птица снова может летать, но мы сбросим в Idle
        }

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

        private void RotateTowards(ref AiContext ctx, float3 direction)
        {
            if (math.lengthsq(direction) < 0.01f) return;

            float3 lookDir = math.normalize(direction);
            quaternion targetRot = quaternion.LookRotation(lookDir, new float3(0f, 1f, 0f));

            ctx.Transform.Rotation = math.slerp(
                ctx.Transform.Rotation,
                targetRot,
                ROTATION_SPEED * ctx.DeltaTime);
        }

        private static float3 SmoothDirection(float3 oldDir, float3 newDir)
        {
            if (math.lengthsq(oldDir) < 0.0001f)
                return newDir;

            float dot = math.dot(math.normalize(oldDir), math.normalize(newDir));
            
            if (dot > DIR_ANGLE_THRESHOLD)
                return oldDir;

            return math.normalize(math.lerp(oldDir, newDir, DIR_SMOOTHING));
        }

        private static bool IsCasting(Entity entity, EntityManager em)
        {
            return em.HasComponent<CastComponent>(entity)
                && em.GetComponentData<CastComponent>(entity).IsActive;
        }

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

        private float3 ApplySeparation(ref AiContext ctx, float3 moveDirection)
        {
            float3 avoidDirection = float3.zero;

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

                float3 diff = ctx.Position - otherPos;
                
                // 🆕 Для летающих разделение можно считать в 3D, но чтобы вороны не 
                // выстраивались в воздушную пробку, оставим по горизонтали
                if (!ctx.Move.IsFlying) diff.y = 0f; 

                float distSq = math.lengthsq(diff);
                float minAllowed = ctx.HitboxRadius + otherHitbox + SEPARATION_BUBBLE;

                if (distSq > 0.0001f && distSq <= minAllowed * minAllowed)
                {
                    float dist = math.sqrt(distSq);
                    float3 pushAway = diff / dist;

                    float strength = 1f - (dist / minAllowed);
                    avoidDirection += pushAway * strength;
                }
            }

            if (math.lengthsq(avoidDirection) < SEPARATION_MIN_STRENGTH)
                return moveDirection;

            avoidDirection = math.normalize(avoidDirection);
            float3 blended = moveDirection * BLEND_MOVE + avoidDirection * BLEND_AVOID;

            float lenSq = math.lengthsq(blended);
            if (lenSq < 0.0001f)
                return moveDirection;

            return math.normalize(blended);
        }
    }
}

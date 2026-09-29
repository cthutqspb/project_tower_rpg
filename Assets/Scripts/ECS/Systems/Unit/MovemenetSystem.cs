using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;

using Physics = UnityEngine.Physics;
using RaycastHit = UnityEngine.RaycastHit;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    public partial struct MovementSystem : ISystem
    {
        private const float Gravity = -21.7f; 
        private const float JumpForce = 4.9f; 

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // 🌍 Кверим вообще всех юнитов (и игрока, и скелетов)
            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<MovementComponent>>().WithNone<IsDeadTag>().WithEntityAccess())
            {
                // 🪦 WOW-КАНOН ИНСТАНТ-СМЕРТИ (Гвард первого кадра гибели):
                // Если в текущем кадре CombatSystem уже опустила ХП в ноль, но IsDeadTag еще лежит в буфере ecb —
                // мы обязаны мгновенно, не дожидаясь конца кадра, затушить всю физику и скорость в ОЗУ чанка!
                if (SystemAPI.HasComponent<HealthComponent>(entity))
                {
                    var health = SystemAPI.GetComponent<HealthComponent>(entity);
                    if (health.Current <= 0f)
                    {
                        var moveWritable = movement.ValueRW;
                        moveWritable.Direction = float3.zero;
                        moveWritable.CurrentSpeed = 0f;
                        moveWritable.JumpRequested = false;
                        movement.ValueRW = moveWritable; // Запекаем покой обратно в чанк
                        
                        continue; // Пулей скипаем все холмы, гравитацию и коллизии! Труп застыл на месте гибели!
                    }
                }
                float3 inputDir = movement.ValueRO.Direction;
                bool isPlayer = SystemAPI.HasComponent<PlayerTag>(entity);

                // =========================================================================
                // 1. ДИНАМИЧЕСКИЙ РЕЙКАСТ ЗЕМЛИ (RAYCAST ВНИЗ С УЧЕТОМ УКЛОНА)
                // =========================================================================
                float groundY = PhysicsUtils.GetGroundHeight(transform.ValueRO.Position);
                bool hitGround = true;          
                

                // 🌟 СГЛАЖИВАНИЕ СПУСКА (GROUND SNAPPING):
                // Если мы НЕ прыгали сами (jumpRequested == false), НЕ летим вверх от старого импульса (direction.y <= 0.1f),
                // но из-за уклона пологих холмов оказались чуть выше земли в воздухе (в пределах 0.3 метра)
                float distanceToGround = transform.ValueRO.Position.y - groundY;
                bool isDescendingHill = hitGround && !movement.ValueRO.JumpRequested && 
                                        (movement.ValueRO.Direction.y <= 0.1f) && 
                                        (distanceToGround > 0f && distanceToGround <= 0.3f);

                if (isDescendingHill)
                {
                    // Нагло и принудительно прижимаем подошвы к холму ДО расчета гравитации и флагов!
                    transform.ValueRW.Position.y = groundY;
                }

                // Флаг приземления проверяется строго после того, как мы сгладили спуск по склону.
                // Допуск на стыки расширен до 0.1м для исключения покадровых иканий.
                bool structurallyGrounded = hitGround && 
                                           (transform.ValueRO.Position.y <= groundY + 0.1f) && 
                                           (movement.ValueRO.Direction.y <= 0.1f);

                // =========================================================================
                // 2. ТВОЯ РОДНАЯ ФИЗИКА: ГРАВИТАЦИЯ И ИМПУЛЬС ПРЫЖКА
                // =========================================================================
                if (!structurallyGrounded)
                {
                    movement.ValueRW.Direction.y += Gravity * dt;
                    movement.ValueRW.IsGrounded = false;
                }
                else
                {
                    // На земле вертикальная скорость мертво стоит в нуле
                    movement.ValueRW.Direction.y = 0f;
                    movement.ValueRW.IsGrounded = true;
                }

                if (movement.ValueRO.JumpRequested && movement.ValueRO.IsGrounded)
                {
                    movement.ValueRW.Direction.y = JumpForce;
                    movement.ValueRW.IsGrounded = false;
                    movement.ValueRW.JumpRequested = false; 
                }

                float3 horizontalDir = new float3(inputDir.x, 0f, inputDir.z);
                bool isMovingHorizontally = math.lengthsq(horizontalDir) > 0f;

                float3 flatMoveVector = float3.zero;

                // =========================================================================
                // 3. WoW-МАТЕМАТИКА ДВИЖЕНИЯ И ВРАЩЕНИЯ ИГРОКА (ОТНОСИТЕЛЬНО КАМЕРЫ)
                // =========================================================================
                if (isPlayer)
                {
                    float3 cameraForward = new float3(math.sin(movement.ValueRO.CameraAngle), 0f, math.cos(movement.ValueRO.CameraAngle));
                    float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x); 

                    float finalSpeed = movement.ValueRO.CurrentSpeed;

                    if (movement.ValueRO.IsRmbOrMmbPressed)
                    {
                        quaternion cameraRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.CameraAngle);
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, cameraRotation, dt * 18f);

                        flatMoveVector = (cameraForward * inputDir.z) + (cameraRight * inputDir.x);
                        if (inputDir.z < 0f) finalSpeed *= 0.5f; 
                    }
                    else
                    {
                        if (inputDir.z < 0f)
                        {
                            quaternion lookAwayRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.CameraAngle);
                            transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookAwayRotation, dt * 14f);

                            flatMoveVector = (-cameraForward) + (cameraRight * inputDir.x);
                            finalSpeed *= 0.5f; 
                        }
                        else
                        {
                            flatMoveVector = (cameraForward * inputDir.z) + (cameraRight * inputDir.x);

                            if (math.lengthsq(flatMoveVector) > 0f)
                            {
                                quaternion lookRotation = quaternion.LookRotation(math.normalize(flatMoveVector), math.up());
                                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, dt * 12f);
                            }
                        }
                    }

                    if (math.lengthsq(flatMoveVector) > 0f)
                    {
                        flatMoveVector = math.normalize(flatMoveVector) * finalSpeed;
                    }
                }
                else
                {
                    // 💀 СКЕЛЕТЫ / МОНСТРЫ
                    if (isMovingHorizontally)
                    {
                        flatMoveVector = math.normalize(horizontalDir) * movement.ValueRO.CurrentSpeed;

                        quaternion lookRotation = quaternion.LookRotation(math.normalize(horizontalDir), math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, dt * 8f);
                    }
                }

                // =========================================================================
                // 4. ПРОВЕРКА ПРЕПЯТСТВИЙ ПЕРЕД НАМИ (АНТИ-ПРИЗРАК)
                // =========================================================================
                if (math.lengthsq(flatMoveVector) > 0f)
                {
                    float3 wallRayStart = new float3(transform.ValueRO.Position.x, transform.ValueRO.Position.y + 0.5f, transform.ValueRO.Position.z);
                    float3 moveDirection = math.normalize(flatMoveVector);

                    if (Physics.Raycast(wallRayStart, moveDirection, out RaycastHit wallHit, 0.5f, 
                        Physics.DefaultRaycastLayers, UnityEngine.QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.collider != null && wallHit.collider.gameObject.name != "Terrain")
                        {
                            flatMoveVector = float3.zero;
                        }
                    }
                }

                // =========================================================================
                // 5. СБОРКА ВЕКТОРА И КОЛЛИЖЕН С ЛАНДШАФТОМ
                // =========================================================================
                float3 finalMoveVector = new float3(flatMoveVector.x, movement.ValueRO.Direction.y, flatMoveVector.z);

                transform.ValueRW.Position += finalMoveVector * dt;

                // Финальная жесткая страховка (если провалились под холм на высокой скорости)
                if (hitGround && transform.ValueRO.Position.y <= groundY && movement.ValueRO.Direction.y <= 0.1f)
                {
                    transform.ValueRW.Position = new float3(transform.ValueRO.Position.x, groundY, transform.ValueRO.Position.z);
                    movement.ValueRW.Direction.y = 0f; 
                    movement.ValueRW.IsGrounded = true;    
                }
            }
        }
    }
}


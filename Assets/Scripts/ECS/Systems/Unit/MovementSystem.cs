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
                if (SystemAPI.HasComponent<HealthComponent>(entity))
                {
                    var health = SystemAPI.GetComponent<HealthComponent>(entity);
                    if (health.Current <= 0f)
                    {
                        var moveWritable = movement.ValueRW;
                        moveWritable.Direction = float3.zero;
                        moveWritable.CurrentSpeed = 0f;
                        moveWritable.JumpRequested = false;
                        movement.ValueRW = moveWritable;
                        
                        continue;
                    }
                }
                
                float3 inputDirection = movement.ValueRO.Direction;
                bool isPlayer = SystemAPI.HasComponent<PlayerTag>(entity);

                // =========================================================================
                // 🦅 1. ЛОГИКА ПОЛЁТА (ПЕРЕХВАТ ДЛЯ ПТИЦ)
                // =========================================================================
                if (movement.ValueRO.IsFlying && movement.ValueRO.CanFly)
                {
                    // 1. Плавно поднимаем/опускаем до высоты полета
                    float targetY = movement.ValueRO.FlyHeight;
                    float currentY = transform.ValueRO.Position.y;
                    
                    // Если ворона ниже нужной высоты — поднимаем, если выше — опускаем
                    float newY = math.lerp(currentY, targetY, dt * 3f); // 3f - скорость подъема

                    // 2. Двигаем по горизонтали (X, Z)
                    float3 flightMove = new float3(inputDirection.x, 0f, inputDirection.z);
                    if (math.lengthsq(flightMove) > 0f)
                    {
                        flightMove = math.normalize(flightMove) * movement.ValueRO.CurrentSpeed;
                    }

                    // 3. Применяем позицию
                    transform.ValueRW.Position = new float3(
                        transform.ValueRO.Position.x + flightMove.x * dt,
                        newY,
                        transform.ValueRO.Position.z + flightMove.z * dt
                    );

                    // 4. Поворачиваем ворону в сторону полёта
                    if (math.lengthsq(flightMove) > 0f)
                    {
                        quaternion lookRotation = quaternion.LookRotation(math.normalize(flightMove), math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, lookRotation, dt * 8f);
                    }

                    // 5. Сбрасываем вертикальную скорость и флаг земли
                    var moveWritable = movement.ValueRW;
                    moveWritable.Direction.y = 0f;
                    moveWritable.IsGrounded = false;
                    movement.ValueRW = moveWritable;

                    continue; // 🛑 ВАЖНО: Пропускаем гравитацию, рейкасты земли и коллизии!
                }

                // =========================================================================
                // 2. ДИНАМИЧЕСКИЙ РЕЙКАСТ ЗЕМЛИ (RAYCAST ВНИЗ С УЧЕТОМ УКЛОНА)
                // =========================================================================
                float groundY = PhysicsUtils.GetGroundHeight(transform.ValueRO.Position);
                bool hitGround = true;          
                
                // 🌟 СГЛАЖИВАНИЕ СПУСКА (GROUND SNAPPING):
                float distanceToGround = transform.ValueRO.Position.y - groundY;
                bool isDescendingHill = hitGround && !movement.ValueRO.JumpRequested && 
                                        (movement.ValueRO.Direction.y <= 0.1f) && 
                                        (distanceToGround > 0f && distanceToGround <= 0.3f);

                if (isDescendingHill)
                {
                    transform.ValueRW.Position.y = groundY;
                }

                bool structurallyGrounded = hitGround && 
                                           (transform.ValueRO.Position.y <= groundY + 0.1f) && 
                                           (movement.ValueRO.Direction.y <= 0.1f);

                // =========================================================================
                // 3. ФИЗИКА: ГРАВИТАЦИЯ И ИМПУЛЬС ПРЫЖКА
                // =========================================================================
                if (!structurallyGrounded)
                {
                    movement.ValueRW.Direction.y += Gravity * dt;
                    movement.ValueRW.IsGrounded = false;
                }
                else
                {
                    movement.ValueRW.Direction.y = 0f;
                    movement.ValueRW.IsGrounded = true;
                }

                if (movement.ValueRO.JumpRequested && movement.ValueRO.IsGrounded)
                {
                    movement.ValueRW.Direction.y = JumpForce;
                    movement.ValueRW.IsGrounded = false;
                    movement.ValueRW.JumpRequested = false; 
                }

                float3 horizontalDirection = new float3(inputDirection.x, 0f, inputDirection.z);
                bool isMovingHorizontally = math.lengthsq(horizontalDirection) > 0f;

                float3 flatMoveVector = float3.zero;

                // =========================================================================
                // 4. WoW-МАТЕМАТИКА ДВИЖЕНИЯ И ВРАЩЕНИЯ ИГРОКА (ОТНОСИТЕЛЬНО КАМЕРЫ)
                // =========================================================================
                if (isPlayer)
                {
                    float3 cameraForward = new float3(math.sin(movement.ValueRO.CameraAngle), 0f, math.cos(movement.ValueRO.CameraAngle));
                    float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x); 

                    float currenSpeed = movement.ValueRO.CurrentSpeed;

                    if (movement.ValueRO.IsRmbOrMmbPressed)
                    {
                        quaternion cameraRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.CameraAngle);
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, cameraRotation, dt * 18f);

                        flatMoveVector = (cameraForward * inputDirection.z) + (cameraRight * inputDirection.x);
                        if (inputDirection.z < 0f) currenSpeed *= 0.5f; 
                    }
                    else
                    {
                        if (inputDirection.z < 0f)
                        {
                            quaternion lookAwayRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.CameraAngle);
                            transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, lookAwayRotation, dt * 14f);

                            flatMoveVector = (-cameraForward) + (cameraRight * inputDirection.x);
                            currenSpeed *= 0.5f; 
                        }
                        else
                        {
                            flatMoveVector = (cameraForward * inputDirection.z) + (cameraRight * inputDirection.x);

                            if (math.lengthsq(flatMoveVector) > 0f)
                            {
                                quaternion lookRotation = quaternion.LookRotation(math.normalize(flatMoveVector), math.up());
                                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, dt * 12f);
                            }
                        }
                    }

                    if (math.lengthsq(flatMoveVector) > 0f)
                    {
                        flatMoveVector = math.normalize(flatMoveVector) * currenSpeed;
                    }
                }
                else
                {
                    // 💀 СКЕЛЕТЫ / МОНСТРЫ
                    if (isMovingHorizontally)
                    {
                        flatMoveVector = math.normalize(horizontalDirection) * movement.ValueRO.CurrentSpeed;

                        quaternion lookRotation = quaternion.LookRotation(math.normalize(horizontalDirection), math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, dt * 8f);
                    }
                }

                // =========================================================================
                // 5. ПРОВЕРКА ПРЕПЯТСТВИЙ ПЕРЕД НАМИ (АНТИ-ПРИЗРАК)
                // =========================================================================
                if (math.lengthsq(flatMoveVector) > 0f)
                {
                    float3 wallRayStart = new float3(transform.ValueRO.Position.x, transform.ValueRO.Position.y + 0.5f, transform.ValueRO.Position.z);
                    float3 moveDirection = math.normalize(flatMoveVector);

                    int unitsLayer = 10;
                    int wallLayerMask = ~(1 << unitsLayer); 

                    if (Physics.Raycast(wallRayStart, moveDirection, out RaycastHit wallHit, 0.5f, 
                        wallLayerMask, UnityEngine.QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.collider != null)
                        {
                            flatMoveVector = float3.zero;
                        }
                    }
                }
                
                // =========================================================================
                // 6. СБОРКА ВЕКТОРА И КОЛЛИЖЕН С ЛАНДШАФТОМ
                // =========================================================================
                float3 moveVector = new float3(flatMoveVector.x, movement.ValueRO.Direction.y, flatMoveVector.z);

                transform.ValueRW.Position += moveVector * dt;

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

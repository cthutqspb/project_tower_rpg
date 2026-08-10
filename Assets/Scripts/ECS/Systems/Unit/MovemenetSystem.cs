using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    public partial class MovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 🌍 Кверим вообще всех (игрока и скелетов)
            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<MovementComponent>>().WithEntityAccess())
            {
                float3 inputDir = movement.ValueRO.direction;
                bool isPlayer = SystemAPI.HasComponent<PlayerTag>(entity);

                if (isPlayer)
                {
                    // Высчитываем базовые направления взгляда камеры на землю
                    float3 cameraForward = new float3(math.sin(movement.ValueRO.cameraAngle), 0f, math.cos(movement.ValueRO.cameraAngle));
                    float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x); 

                    float3 worldMoveVector = float3.zero;
                    float finalSpeed = movement.ValueRO.speed;

                    // =========================================================================
                    // 1. WoW-МАТЕМАТИКА ДВИЖЕНИЯ И ВРАЩЕНИЯ (ЧИСТЫЙ КАНОН С YOUTUBE)
                    // =========================================================================
                    
                    // 🔹 СИТУАЦИЯ А: Зажата ПКМ — персонаж жестко приклеен спиной к камере
                    if (movement.ValueRO.isRmbOrMmbPressed)
                    {
                        quaternion cameraRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.cameraAngle);
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, cameraRotation, deltaTime * 18f);

                        worldMoveVector = (cameraForward * inputDir.z) + (cameraRight * inputDir.x);
                        if (inputDir.z < 0f) finalSpeed *= 0.5f; // штраф на бег задом при ПКМ
                    }
                    
                    // 🔹 СИТУАЦИЯ Б: Мышь отпущена ИЛИ зажата ЛКМ (Истинный WoW-контроль клавиатурой)
                    else
                    {
                        // 🌟 ЕСЛИ НАЖАТА КНОПКА «НАЗАД» (S / S+A / S+D)
                        if (inputDir.z < 0f)
                        {
                            // 1. Корпус МГНОВЕННО разворачивается ЛИЦОМ ВГЛУБЬ ЭКРАНА (спиной к камере!)
                            quaternion lookAwayRotation = quaternion.AxisAngle(math.up(), movement.ValueRO.cameraAngle);
                            transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookAwayRotation, deltaTime * 14f);

                            // 2. Вектор движения принудительно направляем НА КАМЕРУ (к нижнему краю экрана, на тебя!)
                            // При этом подмешиваем стрейфы A/D, если они зажаты вместе с S
                            worldMoveVector = (-cameraForward) + (cameraRight * inputDir.x);
                            
                            // 3. Режем скорость в 2 раза, так как персонаж пятится спиной к камере
                            finalSpeed *= 0.5f;
                        }
                        // 🌟 ЕСЛИ ИГРОК БЕЖИТ ВПЕРЕД ИЛИ СТРЕЙФИТ (W, W+A, W+D, чистые A/D)
                        else
                        {
                            // Вектор идет по направлению камеры
                            worldMoveVector = (cameraForward * inputDir.z) + (cameraRight * inputDir.x);

                            if (math.lengthsq(worldMoveVector) > 0f)
                            {
                                // Персонаж плавно поворачивается лицом КУДА БЕЖИТ (вглубь или в бока)
                                quaternion lookRotation = quaternion.LookRotation(math.normalize(worldMoveVector), math.up());
                                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, deltaTime * 12f);
                            }
                        }
                    }

                    // Применяем итоговое перемещение к игроку в ECS
                    if (math.lengthsq(worldMoveVector) > 0f)
                    {
                        transform.ValueRW.Position += math.normalize(worldMoveVector) * finalSpeed * deltaTime;
                    }
                }
                else
                {
                    // 💀 СКЕЛЕТЫ / МОНСТРЫ: Бегут плавно по вектору ИИ патруля (живые и рабочие)
                    float3 worldMoveVector = inputDir;
                    if (math.lengthsq(worldMoveVector) > 0f)
                    {
                        worldMoveVector = math.normalize(worldMoveVector);
                        transform.ValueRW.Position += worldMoveVector * movement.ValueRO.speed * deltaTime;

                        quaternion lookRotation = quaternion.LookRotation(worldMoveVector, math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, lookRotation, deltaTime * 8f);
                    }
                }

                // =========================================================================
                // 3. ОБРАБОТКА ПРЫЖКА
                // =========================================================================
                if (movement.ValueRO.jumpRequested && movement.ValueRO.isGrounded)
                {
                    movement.ValueRW.jumpRequested = false;
                }
            }
        }
    }
}


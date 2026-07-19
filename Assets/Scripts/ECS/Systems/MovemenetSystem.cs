using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    public partial struct MovementSystem : ISystem
    {
        private const float Gravity = -21.7f; 
        private const float JumpForce = 4.9f; 

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, movement) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<MovementComponent>>())
            {
                // 1. ГРАВИТАЦИЯ (Пишем напрямую в Y нашего монолитного вектора)
                if (!movement.ValueRO.isGrounded)
                {
                    movement.ValueRW.direction.y += Gravity * dt;
                }

                // 2. ИМПУЛЬС ПРЫЖКА
                if (movement.ValueRO.jumpRequested && movement.ValueRO.isGrounded)
                {
                    movement.ValueRW.direction.y = JumpForce;
                    movement.ValueRW.isGrounded = false;
                    movement.ValueRW.jumpRequested = false; 
                }

                // 3. ПРОВЕРКА КНОПОК (Берем только горизонтальные X и Z)
                float3 horizontalDir = new float3(movement.ValueRO.direction.x, 0f, movement.ValueRO.direction.z);
                bool isMovingHorizontally = math.lengthsq(horizontalDir) > 0f;

                // 4. ПОВОРОТ КОРПУСА (Строго на земле)
                if (movement.ValueRO.isGrounded)
                {
                    bool copyCameraRotation = !movement.ValueRO.isLookAroundMode && 
                                             (isMovingHorizontally || movement.ValueRO.isRmbOrMmbPressed);

                    if (copyCameraRotation)
                    {
                        float targetRotationAngle = movement.ValueRO.cameraAngle - math.PI;
                        transform.ValueRW.Rotation = quaternion.AxisAngle(math.up(), targetRotationAngle);
                    }
                }

                // 5. РАСЧЕТ ДВИЖЕНИЯ ОТНОСИТЕЛЬНО КАМЕРЫ
                float3 finalMoveVector = float3.zero;

                if (isMovingHorizontally)
                {
                    quaternion movementBaseRotation;

                    if (movement.ValueRO.isLookAroundMode)
                    {
                        movementBaseRotation = transform.ValueRO.Rotation;
                        float3 rotatedDirection = math.mul(movementBaseRotation, horizontalDir);
                        finalMoveVector = math.normalize(rotatedDirection) * movement.ValueRO.speed;
                    }
                    else
                    {
                        quaternion cameraRot = quaternion.AxisAngle(math.up(), movement.ValueRO.cameraAngle);
                        float3 rotatedDirection = -math.mul(cameraRot, horizontalDir);
                        finalMoveVector = math.normalize(rotatedDirection) * movement.ValueRO.speed;
                    }
                }

                // Вшиваем вертикальную скорость прыжка/падения из нашей координаты Y
                finalMoveVector.y = movement.ValueRO.direction.y;

                // 6. ФИЗИЧЕСКИ СМЕЩАЕМ СУЩНОСТЬ В ОЗУ
                transform.ValueRW.Position += finalMoveVector * dt;

                // 7. КОЛЛИЖЕН ЗЕМЛИ (Фикс высоты через новый float3)
                if (transform.ValueRO.Position.y <= 0f)
                {
                    transform.ValueRW.Position = new float3(transform.ValueRO.Position.x, 0f, transform.ValueRO.Position.z);
                    movement.ValueRW.direction.y = 0f; // Обнуляем координату прыжка
                    movement.ValueRW.isGrounded = true;    
                }
            }
        }
    }
}


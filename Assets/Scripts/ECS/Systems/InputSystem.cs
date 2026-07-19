using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    // 🚀 СВЕРХЗВУКОВОЙ МЕНЕДЖЕД-КЛАСС ВВОДА:
    // Мы заменили struct на class! Варнинг CS0282 навсегда уничтожен в ОЗУ!
    public partial class InputSystem : SystemBase
    {
        private @InputSystem_Actions _inputActions;

        // В SystemBase вместо OnCreate используется OnCreate() без SystemState!
        protected override void OnCreate()
        {
            _inputActions = new @InputSystem_Actions();
            _inputActions.Enable();
        }

        protected override void OnDestroy()
        {
            _inputActions.Disable();
            _inputActions.Dispose();
        }

        protected override void OnUpdate()
        {
            // 1. Выкачиваем чистый Vector2 из сгенерированной карты действий Unity
            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();

            // 2. Конвертируем в убер-быстрый 3D-вектор float3 (ось Y изначально по нулям)
            float3 inputDirection = new float3(moveInput.x, 0f, moveInput.y);

            if (math.lengthsq(inputDirection) > 0)
            {
                inputDirection = math.normalize(inputDirection);
            }

            // 3. Считываем состояние триггера прыжка (Пробел)
            bool jumpPressed = _inputActions.Player.Jump.triggered;

            // 4. Считываем состояния кнопок мыши для нашей WoW/Genshin камеры
            bool isLmbPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool isRmbPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool isMmbPressed = Mouse.current != null && Mouse.current.middleButton.isPressed;

            // 5. 🎰 СНАЙПЕРСКИЙ ЗАПРОС К КЭШУ ПРОЦЕССОРА:
            // Пробегаем по ОЗУ и шёлково заполняем компоненты игрока
            foreach (var movement in SystemAPI.Query<RefRW<MovementComponent>>().WithAll<PlayerTag>())
            {
                // 🔥 КРИТИЧЕСКИЙ ФИКС: Перезаписываем ТОЛЬКО горизонтальный ввод X и Z!
                // Координату Y мы вообще не трогаем, чтобы не занулять расчеты гравитации в MovementSystem!
                movement.ValueRW.direction.x = inputDirection.x;
                movement.ValueRW.direction.z = inputDirection.z;

                // Записываем состояния кнопок мыши
                movement.ValueRW.isLookAroundMode = isLmbPressed && !isRmbPressed && !isMmbPressed;
                movement.ValueRW.isRmbOrMmbPressed = isRmbPressed || isMmbPressed;

                // Если прожали Пробел — взводим флаг для физического толчка вверх
                if (jumpPressed)
                {
                    movement.ValueRW.jumpRequested = true;
                }
            }
        }
    }
}




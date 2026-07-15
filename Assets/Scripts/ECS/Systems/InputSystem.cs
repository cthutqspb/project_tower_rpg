using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
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
            _inputActions = new InputSystem_Actions();
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

            // 2. Конвертируем в убер-быстрый 3D-вектор float3
            float3 inputDirection = new float3(moveInput.x, 0f, moveInput.y);

            if (math.lengthsq(inputDirection) > 0)
            {
                inputDirection = math.normalize(inputDirection);
            }

            // 3. 🎰 СНАЙПЕРСКИЙ ЗАПРОС К КЭШУ ПРОЦЕССОРА:
            // В SystemBase мы используем нативный Entities.ForEach или SystemAPI.Query!
            // Давай бахнем сверхзвуковой Query прямо через SystemAPI!
            foreach (var movement in SystemAPI.Query<RefRW<MovementComponent>>().WithAll<PlayerTag>())
            {
                movement.ValueRW.direction = inputDirection;
            }
        }
    }
}



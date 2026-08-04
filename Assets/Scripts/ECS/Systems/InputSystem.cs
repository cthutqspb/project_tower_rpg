using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using Unity.Transforms;
using ProjectTowerRpg.Core;

namespace ProjectTowerRpg.ECS.Systems
{
    public partial class InputSystem : SystemBase
    {
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _interactOrLookAction;
        private InputAction _actionOrOrbitAction;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            if (_moveAction == null)
            {
                var globalActions = UnityEngine.InputSystem.InputSystem.actions;
                if (globalActions == null) return;

                _moveAction = globalActions.FindAction("Player/Move");
                _jumpAction = globalActions.FindAction("Player/Jump");
                _interactOrLookAction = globalActions.FindAction("Player/InteractOrLook");
                _actionOrOrbitAction = globalActions.FindAction("Player/ActionOrOrbit");

                if (_moveAction == null) return;
            }

            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            float3 inputDirection = new float3(moveInput.x, 0f, moveInput.y);

            if (math.lengthsq(inputDirection) > 0)
            {
                inputDirection = math.normalize(inputDirection);
            }

            // ПРОВЕРКА БЛОКИРОВКИ
            bool isUiBlocked = UIManager.IsBlocked; 

            bool isLmbPressed = !isUiBlocked && _interactOrLookAction.IsPressed();
            bool isRmbPressed = !isUiBlocked && _actionOrOrbitAction.IsPressed();
 
            // ... (Твой стандартный блок OnUpdate с проверкой осей и UIManager.IsBlocked)

                // ОБРАБОТКА КЛИКА В 3D МИРЕ — СТЕРИЛЬНЫЙ ВАРИАНТ
            if (!isUiBlocked && _interactOrLookAction.triggered)
            {
                if (DragManager.Instance != null && !DragManager.Instance.IsDragging)
                {
                    if (SystemAPI.TryGetSingleton<HoverState>(out var hover) && hover.HasTarget)
                    {
                        // ================================================================
                        // 🦾 ПРОВЕРКА ДИСТАНЦИИ ДЛЯ ЛУТА
                        // ================================================================
                        Entity targetEntity = hover.CurrentEntity;
                        
                        // Проверяем, есть ли у цели позиция
                        if (SystemAPI.HasComponent<LocalTransform>(targetEntity))
                        {
                            var targetPos = SystemAPI.GetComponent<LocalTransform>(targetEntity).Position;
                            var playerPos = PlayerUtils.GetPosition();
                            
                            float distance = math.distance(playerPos, targetPos);
                            float maxLootDistance = 0.72f; // можно вынести в конфиг
                            
                            if (distance > maxLootDistance)
                            {
                                // Слишком далеко — не даём клик
                                Debug.Log($"[InputSystem] Слишком далеко до цели ({distance:F1}м). Нужно подойти ближе.");
                                // TODO: показать сообщение на HUD
                                return;
                            }
                        }
                        
                        // Всё ок — отправляем клик
                        EntityManager.AddComponent<ClickIntent>(targetEntity);
                        Debug.Log($"[InputSystem] Послан сигнал клика на Entity ID: {targetEntity.Index}");
                    }
                }
            }

            foreach (var movement in SystemAPI.Query<RefRW<MovementComponent>>().WithAll<PlayerTag>())
            {
                movement.ValueRW.direction.x = inputDirection.x;
                movement.ValueRW.direction.z = inputDirection.z;

                movement.ValueRW.isLookAroundMode = isLmbPressed && !isRmbPressed;
                movement.ValueRW.isRmbOrMmbPressed = isRmbPressed;

                if (_jumpAction.triggered)
                {
                    movement.ValueRW.jumpRequested = true;
                }
            }
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.ECS.Actions; // Добавляем для ActionResolver

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
            // ================================================================
            // 1. ИНИЦИАЛИЗАЦИЯ INPUT ACTIONS (асинхронная)
            // ================================================================
            if (_moveAction == null || _jumpAction == null || _interactOrLookAction == null || _actionOrOrbitAction == null)
            {
                var globalActions = UnityEngine.InputSystem.InputSystem.actions;
                if (globalActions == null) return;

                _moveAction = globalActions.FindAction("Player/Move");
                _jumpAction = globalActions.FindAction("Player/Jump");
                _interactOrLookAction = globalActions.FindAction("Player/InteractOrLook");
                _actionOrOrbitAction = globalActions.FindAction("Player/ActionOrOrbit");

                if (_moveAction == null || _jumpAction == null || _interactOrLookAction == null || _actionOrOrbitAction == null)
                    return;

                Debug.Log("⌨️ [InputSystem]: Все карты ввода синхронизированы!");
            }

            // ================================================================
            // 2. СБОР ДАННЫХ ВВОДА
            // ================================================================
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            float3 inputDirection = new float3(moveInput.x, 0f, moveInput.y);

            if (math.lengthsq(inputDirection) > 0)
                inputDirection = math.normalize(inputDirection);

            bool isUiBlocked = UIManager.IsBlocked;
            bool isLmbPressed = !isUiBlocked && _interactOrLookAction.IsPressed();
            bool isRmbPressed = !isUiBlocked && _actionOrOrbitAction.IsPressed();

            // ================================================================
            // 3. ОБРАБОТКА КЛИКОВ В МИРЕ (ЛКМ и ПКМ)
            // ================================================================

            // ✅ ЗАКРЫВАЕМ КОНТЕКСТНОЕ МЕНЮ ПРИ ЛЮБОМ КЛИКЕ В МИРЕ
            if (!isUiBlocked && (_interactOrLookAction.triggered || _actionOrOrbitAction.triggered))
            {
                UIEvents.TriggerActionsMenuClosed();
            }
             
            if (!isUiBlocked && _interactOrLookAction.triggered)
            { 
                if (DragManager.Instance != null && !DragManager.Instance.IsDragging)
                {
                    if (SystemAPI.TryGetSingleton<HoverState>(out var hover) &&
                        SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                    {
                        var combatState = SystemAPI.GetComponent<CombatStateComponent>(playerEntity);
                        
                        bool isUnit = EntityManager.HasComponent<UnitComponent>(hover.CurrentEntity);
                            
                        if (hover.HasTarget && isUnit)
                        {
                            Entity targetEntity = hover.CurrentEntity;

                            // 🔄 ВСЕГДА обновляем цель в CombatState
                            combatState.CurrentTarget = targetEntity;
                            SystemAPI.SetComponent(playerEntity, combatState);

                            // 🧠 ПЕРЕДАЁМ РЕШЕНИЕ ЭКШЕНА В RESOLVER (Новый слой!)
                            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                            var command = ActionResolver.Resolve(playerEntity, targetEntity, em);

                            if (command.Action != BaseActions.None)
                            {
                                command.SourceEntity = playerEntity; // ← ЭТА СТРОКА БЫЛА ПРОПУЩЕНА

                                var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                                    .CreateCommandBuffer(World.Unmanaged);

                                var cmdEntity = ecb.CreateEntity();
                                ecb.AddComponent(cmdEntity, command);

                                Debug.Log($"[InputSystem] Создана команда '{command.Action}' для цели {targetEntity.Index}");
                            }
                        }
                        else
                        {
                            // Клик в пустоту — сброс цели
                            combatState.CurrentTarget = Entity.Null;
                            SystemAPI.SetComponent(playerEntity, combatState);
                            Debug.Log("[InputSystem]: Цель сброшена (клик в пустоту)");
                        }
                    }
                }
            }

            // ================================================================
            // 4. ПРЫЖОК (ТЕСТОВАЯ ТРАТА МАНЫ)
            // ================================================================
            if (!isUiBlocked && _jumpAction != null && _jumpAction.triggered)
            {
                if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                {
                    if (SystemAPI.HasComponent<ResourceComponent>(playerEntity))
                    {
                        var resource = SystemAPI.GetComponent<ResourceComponent>(playerEntity);
                        resource.Current = math.max(0f, resource.Current - 10f);

                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(World.Unmanaged);

                        ecb.SetComponent(playerEntity, resource);

                        Debug.Log($"[InputSystem] Мана потрачена! Осталось: {resource.Current}/{resource.Max}");
                    }
                }
            }

            // ================================================================
            // 5. ПЕРЕМЕЩЕНИЕ И КАМЕРА
            // ================================================================
            foreach (var movement in SystemAPI.Query<RefRW<MovementComponent>>().WithAll<PlayerTag>())
            {
                movement.ValueRW.direction.x = inputDirection.x;
                movement.ValueRW.direction.z = inputDirection.z;

                movement.ValueRW.isLookAroundMode = isLmbPressed && !isRmbPressed;
                movement.ValueRW.isRmbOrMmbPressed = isRmbPressed;

                if (Camera.main != null)
                {
                    if (!movement.ValueRW.isLookAroundMode)
                    {
                        float cameraRotationYInRadians = Camera.main.transform.eulerAngles.y * math.TORADIANS;
                        movement.ValueRW.cameraAngle = cameraRotationYInRadians;
                    }
                }

                if (_jumpAction.triggered && movement.ValueRO.isGrounded)
                {
                    movement.ValueRW.jumpRequested = true;
                }
            }

            // ================================================================
            // 6. УПРАВЛЕНИЕ КАМЕРОЙ CINEMACHINE
            // ================================================================
            bool isCameraRotatingNow = !isUiBlocked && (isLmbPressed || isRmbPressed);
            var axisController = UnityEngine.Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineInputAxisController>();

            if (axisController != null)
            {
                float baseSensitivity = 27f;

                foreach (var controller in axisController.Controllers)
                {
                    if (controller.Name == "Look Orbit X")
                        controller.Input.Gain = isCameraRotatingNow ? baseSensitivity : 0f;
                    else if (controller.Name == "Look Orbit Y")
                        controller.Input.Gain = isCameraRotatingNow ? -baseSensitivity : 0f;
                }
            }
        }
    }
}

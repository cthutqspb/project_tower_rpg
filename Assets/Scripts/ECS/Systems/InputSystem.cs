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
                        
                        bool isTargetEntity = EntityManager.HasComponent<UnitComponent>(hover.CurrentEntity) || 
                                             EntityManager.HasComponent<ItemComponent>(hover.CurrentEntity);
                            
                        if (hover.HasTarget && isTargetEntity)
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
            // 4. ПРЫЖОК (ТЕСТОВАЯ ТРАТА МАНЫ + УРОН ТАРГЕТУ)
            // ================================================================
            if (!isUiBlocked && _jumpAction != null && _jumpAction.triggered)
            {
                if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                {
                    // --- ТРАТА МАНЫ ИГРОКА ---
                    if (SystemAPI.HasComponent<ResourceComponent>(playerEntity))
                    {
                        var resource = SystemAPI.GetComponent<ResourceComponent>(playerEntity);
                        resource.Current = math.max(0f, resource.Current - 10f);

                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(World.Unmanaged);

                        ecb.SetComponent(playerEntity, resource);
                        Debug.Log($"[InputSystem] Мана потрачена! Осталось: {resource.Current}/{resource.Max}");

                        // --- ⚔️ ХАК: КУСАЕМ ТАРГЕТ НА 10% ОТ МАКС ХП ---
                        if (SystemAPI.HasComponent<CombatStateComponent>(playerEntity))
                        {
                            var combatState = SystemAPI.GetComponent<CombatStateComponent>(playerEntity);
                            Entity targetEntity = combatState.CurrentTarget; // Наш текущий прицел (выбранный скелет)

                            // Проверяем железно: цель вообще выбрана, существует ли она в ОЗУ симуляции и есть ли у неё ХП?
                            if (targetEntity != Entity.Null && EntityManager.Exists(targetEntity) && SystemAPI.HasComponent<HealthComponent>(targetEntity))
                            {
                                var targetHealth = SystemAPI.GetComponent<HealthComponent>(targetEntity);
                                
                                // Вычисляем 10% от МАКСИМАЛЬНОГО здоровья цели
                                float damageAmount = targetHealth.Max * 0.10f;
                                
                                // Нагло срезаем текущее ХП, не падая ниже нуля
                                targetHealth.Current = math.max(0f, targetHealth.Current - damageAmount);

                                // Безопасно пихаем апдейт здоровья цели в тот же unmanaged-конвейер ECB!
                                ecb.SetComponent(targetEntity, targetHealth);

                                Debug.Log($"💥 [InputSystem]: Нанесено {damageAmount} урона цели {targetEntity.Index} при прыжке! Осталось ХП: {targetHealth.Current}/{targetHealth.Max}");
                            }
                        }
                    }
                }
            }

            // ================================================================
            // 5. ПЕРЕМЕЩЕНИЕ И КАМЕРА
            // ================================================================
            foreach (var movement in SystemAPI.Query<RefRW<MovementComponent>>().WithAll<PlayerTag>())
            {
                movement.ValueRW.Direction.x = inputDirection.x;
                movement.ValueRW.Direction.z = inputDirection.z;

                movement.ValueRW.IsLookAroundMode = isLmbPressed && !isRmbPressed;
                movement.ValueRW.IsRmbOrMmbPressed = isRmbPressed;

                if (Camera.main != null)
                {
                    if (!movement.ValueRW.IsLookAroundMode)
                    {
                        float cameraRotationYInRadians = Camera.main.transform.eulerAngles.y * math.TORADIANS;
                        movement.ValueRW.CameraAngle = cameraRotationYInRadians;
                    }
                }

                if (_jumpAction.triggered && movement.ValueRO.IsGrounded)
                {
                    movement.ValueRW.JumpRequested = true;
                }
            }

            // ================================================================
            // 6. УПРАВЛЕНИЕ КАМЕРОЙ CINEMACHINE
            // ================================================================
            bool isCameraRotatingNow = !isUiBlocked && (isLmbPressed || isRmbPressed);
            var axisController = UnityEngine.Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineInputAxisController>();

            if (axisController != null && !DragManager.Instance.IsDragging)
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

            // ================================================================
            // 7. ОБРАБОТКА ХОТКЕЙ (1..=) — ЧИСТЫЙ ECS
            // ================================================================
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Проверяем клавиши 1..=
                for (int i = 0; i < 12; i++)
                {
                    Key key = GetKeyForSlot(i);
                    if (keyboard[key].wasPressedThisFrame)
                    {
                        // Находим игрока
                        if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                        {
                            if (SystemAPI.HasBuffer<ActionBarSlot>(playerEntity))
                            {
                                var barSlots = SystemAPI.GetBuffer<ActionBarSlot>(playerEntity);
                                if (i < barSlots.Length)
                                {
                                    var slot = barSlots[i];
                                    if (!slot.AbilityId.IsEmpty)
                                    {   
                                        UIEvents.TriggerSlotFlash(i);
                                        // 🦾 СОЗДАЁМ КОМАНДУ НА КАСТ (через ECB)
                                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                                            .CreateCommandBuffer(World.Unmanaged);

                                        var requestEntity = ecb.CreateEntity();
                        
                                        // Вытягиваем текущую зафиксированную цель игрока из его компонента целей
                                        // (Подставь сюда точное имя твоего TargetComponent)
                                        Entity playerTarget = SystemAPI.HasComponent<CombatStateComponent>(playerEntity)
                                            ? SystemAPI.GetComponent<CombatStateComponent>(playerEntity).CurrentTarget
                                            : Entity.Null;
                                        // Накатываем структуру ММО-запроса копейка в копейку под наш компонент!
                                        ecb.AddComponent(requestEntity, new CastRequest
                                        {
                                            Player = playerEntity,
                                            SlotIndex = i,
                                            AbilityId = slot.AbilityId,
                                            TargetEntity = playerTarget // 🔥 Цель намертво зафиксирована!
                                        });

                                        Debug.Log($"[InputSystem] Хоткей {i + 1}: {slot.AbilityId} отправлен в ОЗУ. Фиксированный таргет: {playerTarget}");
                                        break;
                                   }
                                }
                            }
                        }
                    }
                }
            }

        }

        private Key GetKeyForSlot(int slot)
        {
            return slot switch
            {
                0 => Key.Digit1,
                1 => Key.Digit2,
                2 => Key.Digit3,
                3 => Key.Digit4,
                4 => Key.Digit5,
                5 => Key.Digit6,
                6 => Key.Digit7,
                7 => Key.Digit8,
                8 => Key.Digit9,
                9 => Key.Digit0,
                10 => Key.Minus,
                11 => Key.Equals,
                _ => Key.None
            };
        }
    }
}

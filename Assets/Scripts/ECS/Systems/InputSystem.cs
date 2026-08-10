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
            // 🎯 ИСПРАВЛЕНО НАМЕРТВО (АСИНХРОННЫЙ ГВАРД ВВОДА):
            // Проверяем ВСЕ экшены сразу. Если хоть один равен null — 
            // мы покадрово опрашиваем менеджер ввода, пока все ссылки не пропишутся в RAM!
            if (_moveAction == null || _jumpAction == null || _interactOrLookAction == null || _actionOrOrbitAction == null)
            {
                var globalActions = UnityEngine.InputSystem.InputSystem.actions;
                if (globalActions == null) return;

                _moveAction = globalActions.FindAction("Player/Move");
                _jumpAction = globalActions.FindAction("Player/Jump");
                _interactOrLookAction = globalActions.FindAction("Player/InteractOrLook");
                _actionOrOrbitAction = globalActions.FindAction("Player/ActionOrOrbit");

                // Замок: выходим только если сборка экшенов не завершена
                if (_moveAction == null || _jumpAction == null || _interactOrLookAction == null || _actionOrOrbitAction == null) 
                    return;

                Debug.Log("⌨️ [InputSystem]: Все ААА-карты ввода шёлково засинхронизированы с ОЗУ!");
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

                // Ваши флаги мыши
                movement.ValueRW.isLookAroundMode = isLmbPressed && !isRmbPressed;
                movement.ValueRW.isRmbOrMmbPressed = isRmbPressed;

                if (Camera.main != null)
                {
                    // 🌟 ИСПРАВЛЕНИЕ ДЛЯ ЛКМ:
                    // Если игрок зажал ЛКМ (LookAround), мы ЗАМОРАЖИВАЕМ угол движения.
                    // Персонаж будет бежать по тому углу, который был в момент нажатия кнопки, 
                    // пока мышь свободно крутит камеру вокруг него!
                    if (!movement.ValueRW.isLookAroundMode)
                    {
                        float cameraRotationYInRadians = Camera.main.transform.eulerAngles.y * math.TORADIANS;
                        movement.ValueRW.cameraAngle = cameraRotationYInRadians;
                    }
                }

                if (_jumpAction.triggered)
                {
                    movement.ValueRW.jumpRequested = true;
                }
            }


                        // =========================================================================
            // 🎛️ ДИНАМИЧЕСКИЙ КОНТРОЛЬ И СКОРОСТЬ КАМЕРЫ CINEMACHINE V3 (UNITY 6.6)
            // =========================================================================
            // Проверяем: зажата ли ЛКМ или ПКМ прямо сейчас
            bool isCameraRotatingNow = !isUiBlocked && (isLmbPressed || isRmbPressed);

            // Находим контроллер осей Cinemachine на сцене в главном потоке
            var axisController = UnityEngine.Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineInputAxisController>();

            if (axisController != null)
            {
                // В будущем эти две константы вы пропишете в конфиг или SettingsManager:
                float baseSensitivity = 27f; 
                
                foreach (var controller in axisController.Controllers)
                {
                    // Вращение ВЛЕВО-ВПРАВО (Ось X)
                    if (controller.Name == "Look Orbit X")
                    {
                        controller.Input.Gain = isCameraRotatingNow ? baseSensitivity : 0f;
                    }
                    // Наклон ВВЕРХ-ВНИЗ (Ось Y)
                    else if (controller.Name == "Look Orbit Y")
                    {
                        // 🌟 ИНВЕРСИЯ ПО КАНОНУ WoW: Ставим знак минус перед чувствительностью.
                        // Тянем мышь вниз — камера плавно опускается к земле, открывая топ-даун вид.
                        controller.Input.Gain = isCameraRotatingNow ? -baseSensitivity : 0f;
                    }
                }
            }
        }
    }
}


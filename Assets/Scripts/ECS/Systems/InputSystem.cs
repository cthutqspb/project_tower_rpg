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

                        // =========================================================================
            // 🖱️ УНИВЕРСАЛЬНЫЙ WoW-КЛИК ПО ЛКМ (Интеракт + Выделение Цели)
            // =========================================================================
            if (!isUiBlocked && _interactOrLookAction.triggered)
            {
                if (DragManager.Instance != null && !DragManager.Instance.IsDragging)
                {
                    if (SystemAPI.TryGetSingleton<HoverState>(out var hover) && 
                        SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                    {
                        var combatState = SystemAPI.GetComponent<CombatStateComponent>(playerEntity);

                        if (hover.HasTarget)
                        {
                            Entity targetEntity = hover.CurrentEntity;

                            // 👥 ФИЛЬТР А: КЛИКНУЛИ В ЮНИТА (Скелет / NPC)
                            if (SystemAPI.HasComponent<UnitComponent>(targetEntity))
                            {
                                // Взятие в таргет работает ВСЕГДА и с любого расстояния!
                                combatState.CurrentTarget = targetEntity;
                                SystemAPI.SetComponent(playerEntity, combatState);
                                Debug.Log($"[InputSystem]: Цель-ЮНИТ записана в CombatState игрока! Индекс: {targetEntity.Index}");

                                // TODO ДАЛЕКО В БУДУЩЕМ: Если дистанция в упор — можно сразу запускать автоатаку
                            }

                            // 📦 ФИЛЬТР Б: КЛИКНУЛИ В ПРЕДМЕТ (Лут / Куб)
                            else if (SystemAPI.HasComponent<ItemComponent>(targetEntity))
                            {
                                // Предметы тоже можно брать в таргет по канону (или нет, но мы пишем для задела)
                                combatState.CurrentTarget = targetEntity;
                                SystemAPI.SetComponent(playerEntity, combatState);
                                Debug.Log($"[InputSystem]: Цель-ПРЕДМЕТ записана в CombatState игрока! Индекс: {targetEntity.Index}");

                                // А вот ЛУТАТЬ предмет разрешаем СТРОГО в упор!
                                if (SystemAPI.HasComponent<LocalTransform>(targetEntity))
                                {
                                    var targetPos = SystemAPI.GetComponent<LocalTransform>(targetEntity).Position;
                                    var playerPos = PlayerUtils.GetPosition();
                                    float distance = math.distance(playerPos, targetPos);
                                    float maxLootDistance = 0.72f;

                                    if (distance <= maxLootDistance)
                                    {
                                        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
                                        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

                                        ecb.AddComponent(targetEntity, new ClickIntent { Actor = playerEntity });
                                        Debug.Log($"[InputSystem] В упор! Послан ClickIntent на Предмет ID: {targetEntity.Index}");
                                    }
                                    else
                                    {
                                        Debug.Log($"[InputSystem] Слишком далеко до предмета ({distance:F1}м). Нужно подойти ближе.");
                                    }
                                }
                            }

                            // 🧱 ФИЛЬТР В: ДАЛЕКО В БУДУЩЕМ (ObjectComponent / Интерактивные двери / Рычаги)
                            /*
                            else if (SystemAPI.HasComponent<ObjectComponent>(targetEntity))
                            {
                                // Логика рычагов и дверей...
                            }
                            */
                        }
                        else
                        {
                            // Кликнули в пустоту (земля/небо) — сбрасываем таргет игрока
                            combatState.CurrentTarget = Entity.Null;
                            SystemAPI.SetComponent(playerEntity, combatState);
                            Debug.Log("[InputSystem]: Цель игрока сброшена в Entity.Null (Клик в пустоту)");
                        }
                    }
                }
            }


            // =========================================================================
            // 🦾 ТЕСТ-ХАК ТРАТЫ МАНЫ ЧЕРЕЗ СТЕРИЛЬНЫЙ ECB (Конец симуляции)
            // =========================================================================
            if (!isUiBlocked && _jumpAction != null && _jumpAction.triggered)
            {
                if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
                {
                    if (SystemAPI.HasComponent<ResourceComponent>(playerEntity))
                    {
                        var resource = SystemAPI.GetComponent<ResourceComponent>(playerEntity);

                        // Скручиваем ману на 10 единиц
                        resource.Current = math.max(0f, resource.Current - 10f);

                        // 🌟 ЗАПИСЬ ЧЕРЕЗ СИСТЕМНЫЙ БУФЕР КОМАНД (Фикс DidChange для Презентации)
                        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
                        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

                        // Отправляем команду перезаписи компонента через барьер конца симуляции.
                        // На стыке кадров ECB зальет данные, намертво сдвинет версию чанка,
                        // и твоя UIPullSystem в PresentationSystemGroup шёлково поймает DidChange!
                        ecb.SetComponent(playerEntity, resource);

                        Debug.Log($"[ECS InputSystem]: Записали трату маны через ECB! Осталось: {resource.Current}/{resource.Max}");
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

                // 🌟 ИСПРАВЛЕНО НАМЕРТВО: Защита от спама Пробела в воздухе
                // Записываем запрос на прыжок ТОЛЬКО если персонаж уже приземлился и occupies_ground
                if (_jumpAction.triggered && movement.ValueRO.isGrounded)
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


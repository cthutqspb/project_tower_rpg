using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI
{
    public class UIInputHandler : MonoBehaviour
    {   
        private InputAction _toggleCharacterAction;
        private InputAction _closeWindowAction;
        private EntityManager _entityManager;
        private TooltipVisual _tooltipVisual;

        private void Start()
        {
            var actions = UnityEngine.InputSystem.InputSystem.actions;
            _toggleCharacterAction = actions.FindAction("UI/ToggleCharacterWindow");
            _closeWindowAction = actions.FindAction("UI/CloseWindow");

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            UIEvents.OnUiClick += HandleUiClick;
            UIEvents.OnActionsMenuClosed += ActionsMenu.Hide;

            var panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (panelRenderer != null)
            {
                panelRenderer.RegisterUIReloadCallback((renderer, root, version) => 
                {
                    if (root == null) return;

                    // ✅ МЕХАНИКА BG3: Ловим любые "бесхозные" клики в UI, которые долетели до корня
                    // Оставляем обычную фазу (без TrickleDown)
                    root.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        // Если меню открыто, и клик долетел до корня (его никто не перехватил по пути)
                        if (ActionsMenu.IsVisible())
                        {
                            // Просто закрываем меню
                            UIEvents.TriggerActionsMenuClosed();
                            
                            // Поглощаем клик, чтобы он не улетел в 3D мир
                            evt.StopPropagation();
                        }
                    });

                    _tooltipVisual = new TooltipVisual();
                    root.Add(_tooltipVisual);
                });
            }

        }

        private void OnDestroy()
        {
            UIEvents.OnUiClick -= HandleUiClick;
            UIEvents.OnActionsMenuClosed -= ActionsMenu.Hide;
        }

        private void Update()
        {
            if (_toggleCharacterAction.triggered) UIEvents.TriggerToggleCharacterWindow();
            
            // ✅ ESC: СНАЧАЛА КОНТЕКСТНОЕ МЕНЮ, ПОТОМ ОКНА
            if (_closeWindowAction.triggered)
            {
                if (ActionsMenu.IsVisible())
                {
                    ActionsMenu.Hide();
                }
                else
                {
                    WindowManager.CloseTop();
                }
            }

            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TooltipManager.UpdateMouse(mousePos.x, mousePos.y);
            }

            if (_tooltipVisual != null) _tooltipVisual.UpdateTick();
        }

        // ================================================================
        // 🎯 УНИВЕРСАЛЬНЫЙ ДАБЛКЛИК (ЧЕРЕЗ ECS ТЕГИ)
        // ================================================================
        private void HandleUiDoubleClick(UiClickContext context)
        {
            if (context.ContextEntity == Entity.Null) return;

            var em = _entityManager;

            bool isInventory = em.HasComponent<InventoryTag>(context.ContextEntity);
            bool isPaperdoll = em.HasComponent<PaperdollTag>(context.ContextEntity);
            bool isContainer = em.HasComponent<ContainerTag>(context.ContextEntity);

            Entity targetContainerEntity = Entity.Null;

            // КЕЙС 1: ИНВЕНТАРЬ → кукла
            if (isInventory && !isContainer)
            {
                var ownerEntity = em.GetComponentData<ContainerConfigComponent>(context.ContextEntity).Owner;

                var allContainers = em.CreateEntityQuery(
                    ComponentType.ReadOnly<ContainerConfigComponent>(),
                    ComponentType.ReadOnly<PaperdollTag>()
                ).ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var container in allContainers)
                {
                    var cfg = em.GetComponentData<ContainerConfigComponent>(container);
                    if (cfg.Owner == ownerEntity)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                }
                allContainers.Dispose();

                if (targetContainerEntity != Entity.Null)
                {
                    CreateTransferCommand(em, context.ContextEntity, context.SlotIndex, targetContainerEntity, context.TargetId, context.Amount);
                    Debug.Log($"[UIInputHandler] Даблклик: экипировка {context.TargetId}");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найдена кукла для {context.TargetId}");
                return;
            }

            // КЕЙС 2: КУКЛА → инвентарь
            if (isPaperdoll)
            {
                var ownerEntity = em.GetComponentData<ContainerConfigComponent>(context.ContextEntity).Owner;

                var allContainers = em.CreateEntityQuery(
                    ComponentType.ReadOnly<ContainerConfigComponent>(),
                    ComponentType.ReadOnly<InventoryTag>()
                ).ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var container in allContainers)
                {
                    var cfg = em.GetComponentData<ContainerConfigComponent>(container);
                    if (cfg.Owner == ownerEntity)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                }
                allContainers.Dispose();

                if (targetContainerEntity != Entity.Null)
                {
                    CreateTransferCommand(em, context.ContextEntity, context.SlotIndex, targetContainerEntity, context.TargetId, context.Amount);
                    Debug.Log($"[UIInputHandler] Даблклик: снятие {context.TargetId}");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найден инвентарь для снятия {context.TargetId}");
                return;
            }

            // КЕЙС 3: КОНТЕЙНЕР (сундук/матрешка) → инвентарь игрока
            if (isContainer)
            {
                Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (playerEntity != Entity.Null)
                {
                    targetContainerEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(playerEntity, em);
                }

                if (targetContainerEntity != Entity.Null)
                {
                    CreateTransferCommand(em, context.ContextEntity, context.SlotIndex, targetContainerEntity, context.TargetId, context.Amount);
                    Debug.Log($"[UIInputHandler] Даблклик: забрал {context.TargetId} из контейнера");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найден инвентарь игрока для {context.TargetId}");
                return;
            }

            Debug.LogWarning($"[UIInputHandler] Неизвестный тип контейнера для даблклика: {context.ContextEntity}");
        }

        // ================================================================
        // 🎯 ЕДИНЫЙ ОБРАБОТЧИК КЛИКОВ В UI
        // ================================================================
        private void HandleUiClick(UiClickContext context)
        {
            // ✅ ПРАВИЛО ПОГЛОЩЕНИЯ: Если событие дошло сюда при открытом меню, 
            // значит игрок гарантированно кликнул МИМО самого меню.
            if (ActionsMenu.IsVisible())
            {
                ActionsMenu.Hide();

                // Как в WoW: одиночный ЛКМ мимо меню просто скрывает его,
                // выходим сразу, чтобы случайно не прожать элемент под меню.
                if (context.MouseButton == 0 && context.ClickCount == 1)
                {
                    return;
                }
            }

            if (context.ContextEntity == Entity.Null) return;

            // ------------------------------------------------------------
            // 🖱️ ОБРАБОТКА ЛКМ (MouseButton == 0)
            // ------------------------------------------------------------
            if (context.MouseButton == 0)
            {
                // Нас интересует только Даблклик ЛКМ для трансфера предметов
                if (context.ClickCount == 2)
                {
                    HandleUiDoubleClick(context);
                }
                return;
            }

            // ------------------------------------------------------------
            // 🖱️ ОБРАБОТКА ПКМ (MouseButton == 1)
            // ------------------------------------------------------------
            if (context.MouseButton == 1 && context.ClickCount == 1)
            {
                var em = _entityManager;

                // Аурафрейм (не открывает меню, шлёт ECS-команду)
                if (em.HasComponent<AuraFrameTag>(context.ContextEntity))
                {
                    var actionEntity = em.CreateEntity();
                    em.AddComponentData(actionEntity, new ActionCommand
                    {
                        ActionType = "disable_aura",
                        SourceEntity = context.ContextEntity,
                        SourceSlot = context.SlotIndex,
                        ItemId = context.TargetId
                    });
                    return;
                }

                // Экшенбар (пока игнорируем)
                if (em.HasComponent<ActionBarTag>(context.ContextEntity))
                {
                    return;
                }

                Debug.Log("[UIInputHandler] Перенаправляем контекст в ActionsMenu");
                
                // Для инвентаря, куклы и лута — просто открываем меню
                ActionsMenu.Show(context);
            }
        }

        // ================================================================
        // 🛠️ ВСПОМОГАТЕЛЬНЫЙ МЕТОД
        // ================================================================
        private void CreateTransferCommand(EntityManager em, Entity source, int sourceSlot, Entity target, string itemId, int amount)
        {
            var actionEntity = em.CreateEntity();
            em.AddComponentData(actionEntity, new ActionCommand
            {
                ActionType = "item_transfer",
                SourceEntity = source,
                SourceSlot = sourceSlot,
                TargetEntity = target,
                TargetSlot = -1,
                ItemId = itemId,
                Amount = amount
            });
        }
    }
}

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
            UIEvents.OnMenuActionSelected += ActionsMenu.HandleMenuActionSelected;

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
            UIEvents.OnMenuActionSelected -= ActionsMenu.HandleMenuActionSelected;
        }

        private void Update()
        {
            if (_toggleCharacterAction.triggered) UIEvents.TriggerOpenWindow(WindowType.Character);
            
            // ✅ ESC: СНАЧАЛА КОНТЕКСТНОЕ МЕНЮ, ПОТОМ ОКНА, ПОТОМ ГЛАВНОЕ МЕНЮ
            if (_closeWindowAction.triggered)
            {
                if (ActionsMenu.IsVisible())
                {
                    ActionsMenu.Hide();
                }
                // Предполагаем, что твой WindowManager.CloseTop() возвращает bool:
                // true — если он успешно закрыл окно из стека, false — если стек окон уже был пуст!
                // Если у тебя метод void, то гвард пишется через проверку: WindowManager.HasOpenWindows
                else if (WindowManager.IsAnyOpen)
                {
                    WindowManager.CloseTop();
                }
                else
                {
                    // 🦾 ФИНАЛЬНЫЙ РУБЕЖ: Окон нет, меню скрыто — шёлково триггерим Главное Меню!
                    UIEvents.TriggerToggleMainMenu();
                }
            }

            if (Mouse.current != null)
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                TooltipManager.UpdateMouse(mousePosition.x, mousePosition.y);
            }

            if (_tooltipVisual != null) _tooltipVisual.UpdateTick();
        }


        // ================================================================
// 🎯 УНИВЕРСАЛЬНЫЙ ДАБЛКЛИК (РЕЗОЛВ ЧЕРЕЗ BuffersLinkComponent)
// ================================================================
private void HandleUiDoubleClick(UiClickContext context)
{
    if (context.ContextEntity == Entity.Null) return;

    var em = _entityManager;

    // 🦾 1. РЕЗОЛВ SOURCE: если есть BuffersLink.Inventory — берём его.
    //         Иначе работаем с самим entity (это bagEntity или старый стиль).
    Entity sourceContainer = context.ContextEntity;
    if (em.HasComponent<BuffersLinkComponent>(context.ContextEntity))
    {
        var links = em.GetComponentData<BuffersLinkComponent>(context.ContextEntity);
        if (links.Inventory != Entity.Null && em.Exists(links.Inventory))
            sourceContainer = links.Inventory;
    }

    // 🦾 2. РЕЗОЛВ ИГРОКА
    Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
    if (playerEntity == Entity.Null || !em.HasComponent<BuffersLinkComponent>(playerEntity))
    {
        Debug.LogWarning("[UIInputHandler] Нет игрока или у него нет BuffersLinkComponent");
        return;
    }

    var playerLinks = em.GetComponentData<BuffersLinkComponent>(playerEntity);
    Entity playerInventory = playerLinks.Inventory;
    Entity playerPaperdoll = playerLinks.Paperdoll;

    // 🦾 3. РЕЗОЛВ TARGET по правилу:
    //    - source == inventory игрока   → target = paperdoll (экипировка)
    //    - source == paperdoll игрока   → target = inventory (снятие)
    //    - иначе (чужой контейнер/лут)  → target = inventory игрока
    Entity targetContainer;
    if (sourceContainer == playerInventory)
        targetContainer = playerPaperdoll;
    else if (sourceContainer == playerPaperdoll)
        targetContainer = playerInventory;
    else
        targetContainer = playerInventory;

    if (targetContainer == Entity.Null || !em.Exists(targetContainer))
    {
        Debug.LogWarning($"[UIInputHandler] Не найден target для source {sourceContainer}");
        return;
    }

    // 🦾 4. ШЛЁМ КОМАНДУ
    CreateTransferCommand(
        em,
        sourceContainer,
        context.SlotIndex,
        targetContainer,
        context.TargetId,
        context.Amount,
        -1
    );

    Debug.Log($"[UIInputHandler] Даблклик: {context.TargetId} из {sourceContainer} в {targetContainer}");
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
                // Одиночный клик ЛКМ — активация способности из буфера Экшенбара!
                if (context.ClickCount == 1)
                {
                    var em = _entityManager;
                    Entity unitEntity = context.ContextEntity; // Живая Entity Игрока/Юнита

                    if (unitEntity != Entity.Null && em.Exists(unitEntity))
                    {
                        // 🦾 СИ-КАНОН: Проверяем наличие буфера хоткеев напрямую на сущности!
                        // (Если у тебя в UiClickContext зашито поле типа слота, например context.IsActionBarSlot, 
                        // добавь его в гвард, чтобы наглухо исключить пересечение с инвентарем)
                        if (em.HasBuffer<ActionBarSlot>(unitEntity))
                        {
                            var actionBar = em.GetBuffer<ActionBarSlot>(unitEntity);
                            int slotIdx = context.SlotIndex; // Точный индекс ячейки (0..11)

                            if (slotIdx >= 0 && slotIdx < actionBar.Length)
                            {
                                var slot = actionBar[slotIdx];
                                
                                // Если в ячейке ОЗУ лежит валидный ID спелла — шлепаем ММО-запрос!
                                if (!slot.AbilityId.IsEmpty)
                                {
                                    // Вытягиваем текущую цель из TargetComponent
                                    // Entity playerTarget = em.HasComponent<TargetComponent>(unitEntity) 
                                    //     ? em.GetComponentData<TargetComponent>(unitEntity).Value 
                                    //     : Entity.Null;
                                    //
                                    // Рождаем слепой Си-пакет запроса на каст в память симуляции!
                                    Entity requestEntity = em.CreateEntity();
                                    // em.AddComponentData(requestEntity, new CastRequest 
                                    //     _abilityId = slot.AbilityId,
                                    //     _targetEntity = playerTarget
                                    // });

                                    Debug.Log($"[UIInputHandler]: ЛКМ по буферу хоткеев! Слот: {slotIdx + 1}, Спелл: {slot.AbilityId}");
                                    return; // Поглощаем инпут, клик зафиксирован успешно!
                                }
                            }
                        }
                    }
                }

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
                        Action = CombatActions.AuraDisable,
                        SourceEntity = context.ContextEntity,
                        SourceSlot = context.SlotIndex,
                        ItemId = context.TargetId
                    });
                    return;
                }

                // Экшенбар (пока игнорируем)
                // if (em.HasComponent<ActionBarTag>(context.ContextEntity))
                // {
                //     return;
                // }

                Debug.Log("[UIInputHandler] Перенаправляем контекст в ActionsMenu");
                
                // Для инвентаря, куклы и лута — просто открываем меню
                ActionsMenu.Show(context);
            }
        }

        // ================================================================
        // 🛠️ ВСПОМОГАТЕЛЬНЫЙ МЕТОД
        // ================================================================
        private void CreateTransferCommand(EntityManager em, Entity source, int sourceSlot, Entity target, string itemId, int amount, int targetSlot = -1)
        {
            var actionEntity = em.CreateEntity();
            em.AddComponentData(actionEntity, new ActionCommand
            {
                Action = ItemActions.Transfer,
                SourceEntity = source,
                SourceSlot = sourceSlot,
                TargetEntity = target,
                TargetSlot = targetSlot,
                ItemId = itemId,
                Amount = amount
            });
        }
    }
}

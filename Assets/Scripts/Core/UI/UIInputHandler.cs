using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Systems;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI
{
    public class UIInputHandler : MonoBehaviour
    {
        private InputAction _toggleCharacterAction;
        private InputAction _closeWindowAction;
        private EntityManager _entityManager;
        
        // Ссылка на верхний визуальный слой тултипа (Аналог LAYERS.TOOLTIP из Defold)
        private TooltipVisual _tooltipVisual;

        private void Start()
        {
            var actions = UnityEngine.InputSystem.InputSystem.actions;
            _toggleCharacterAction = actions.FindAction("UI/ToggleCharacterWindow");
            _closeWindowAction = actions.FindAction("UI/CloseWindow");

            // Кэшируем менеджер сущностей ECS
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 🎯 ПОДПИСЫВАЕМСЯ НА СЛЕПЫЕ СОБЫТИЯ СЛОТОВ (Твои msg.post аналоги)
            UIEvents.OnSlotDoubleClick += HandleSlotDoubleClick;
            UIEvents.OnSlotRightClick += HandleSlotRightClick;

            // 🧱 ИНИЦИАЛИЗАЦИЯ ВЕРХНЕГО СЛОЯ ТУЛТИПОВ В UI TOOLKIT
            var panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (panelRenderer != null)
            {
                panelRenderer.RegisterUIReloadCallback((renderer, root, version) => 
                {
                    if (root == null) return;
                    
                    // Создаем визуал тултипа и добавляем его в самый конец глобального корня.
                    // В UI Toolkit элементы, добавленные последними, гарантированно рендерятся поверх всех окон!
                    _tooltipVisual = new TooltipVisual();
                    root.Add(_tooltipVisual);
                });
            }
        }

        private void OnDestroy()
        {
            // Железно отписываемся при уничтожении объекта, чтобы не плодить утечки памяти
            UIEvents.OnSlotDoubleClick -= HandleSlotDoubleClick;
            UIEvents.OnSlotRightClick -= HandleSlotRightClick;
        }

        private void Update()
        {
            // 1. Обработка системных горячих клавиш окон
            if (_toggleCharacterAction.triggered)
            {
                UIEvents.TriggerToggleCharacterWindow();
            }

            if (_closeWindowAction.triggered)
            {
                WindowManager.CloseTop();
            }

            // 2. Обновляем координаты мыши в менеджере сессий тултипов (Каждый кадр)
            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TooltipManager.UpdateMouse(mousePos.x, mousePos.y);
            }

            // 3. Запускаем пассивный тик отрисовки тултипа (Твой оригинальный update из Defold)
            if (_tooltipVisual != null)
            {
                _tooltipVisual.UpdateTick();
            }
        }

        // ================================================================
        // 🎒 КAСКAД ДАБЛКЛИКА (АВТО-ПЕРЕНОС И ЭКИПИРОВКА)
        // ================================================================
        private void HandleSlotDoubleClick(SlotElement slot, int index, string gridType, string itemId, int amount)
        {
            if (slot.ContainerEntity == Entity.Null) return;

            var em = _entityManager;
            var actionEntity = em.CreateEntity();

            // 🪐 КАНOНИЧНЫЙ БEЗРEEСТРOВЫЙ ПОИСК В ОЗУ:
            // 1. Смотрим, какому Юниту (хозяину) принадлежит этот контейнер
            var currentConfig = em.GetComponentData<ContainerConfigComponent>(slot.ContainerEntity);
            Entity unitOwner = currentConfig.Owner;

            Entity targetContainerEntity = Entity.Null;

            // 2. Ищем парный контейнер ЭТОГО ЖЕ САМОГО ЮНИТА прямо в памяти ECS
            var containerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ContainerConfigComponent>());
            var allContainers = containerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var container in allContainers)
            {
                var cfg = em.GetComponentData<ContainerConfigComponent>(container);
                if (cfg.Owner == unitOwner)
                {
                    // Если кликнули в рюкзаке (inventory), нам нужна его же КУКЛА (Rows == 1)
                    if (gridType == "inventory" && cfg.Rows == 1)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                    // Если кликнули в кукле, нам нужен его же РЮКЗАК (Rows > 1)
                    if (gridType == "paperdoll" && cfg.Rows > 1)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                }
            }
            allContainers.Dispose();

            // Если парный контейнер не нашелся (например, у NPC нет куклы), стопаем
            if (targetContainerEntity == Entity.Null)
            {
                Debug.LogWarning($"[UIInputHandler] Не найден парный контейнер для юнита {unitOwner}!");
                return;
            }

            // 3. ПУШИМ КОМАНДУ ТРАНСФЕРА С ЧЕСТНЫМИ СУЩНОСТЯМИ ИЗ ОЗУ
            if (gridType == "inventory")
            {
                // ================================================================
                // TODO: Твой каноничный тернарник / switch на будущее для кейсов Торговли и Сундуков
                // var finalTarget = InteractionManager.HasActiveTarget() ? ... : targetContainerEntity;
                // ================================================================

                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "item_transfer",
                    SourceEntity = slot.ContainerEntity, // Наш рюкзак
                    SourceSlot = index,
                    TargetEntity = targetContainerEntity, // Наша кукла, вычисленная из ОЗУ!
                    TargetSlot = -1, 
                    ItemId = itemId,
                    Amount = amount 
                });
                Debug.Log($"[UIInputHandler] Даблклик: запрос экипировки {itemId} отправлен в ECS.");
            }
            else if (gridType == "paperdoll")
            {
                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "item_transfer",
                    SourceEntity = slot.ContainerEntity, // Наша кукла
                    SourceSlot = index,
                    TargetEntity = targetContainerEntity, // Наш рюкзак, вычисленный из ОЗУ!
                    TargetSlot = -1, 
                    ItemId = itemId,
                    Amount = amount
                });
                Debug.Log($"[UIInputHandler] Даблклик: запрос снятия {itemId} отправлен в ECS.");
            }
        }


        // ================================================================
        // 🔮 КAСКAД ПКМ (ТВОЙ РОДНОЙ ФИРМЕННЫЙ СТЕК ИЗ DEFOLD)
        // ================================================================
        private void HandleSlotRightClick(SlotElement slot, int index, string gridType, string itemId, int amount, Vector2 mousePos)
        {
            // 🦠 КAСКAД АYР (ПКМ снятие баффов по WoW-канону):
            if (gridType == "aura_frame")
            {
                var actionEntity = _entityManager.CreateEntity();
                _entityManager.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "disable_aura", 
                    SourceEntity = slot.ContainerEntity,
                    SourceSlot = index,
                    ItemId = itemId
                });
                Debug.Log($"[UIInputHandler] ПКМ по ауре: запрос на принудительное снятие баффа [{itemId}]");
                return;
            }

            // ⚔️ КAСКAД ЭКШEН-БAРA (ПКМ на панели способностей наглухо игнорируется рантаймом):
            if (gridType == "action_bar")
            {
                return;
            }

            // 🎒 Backpack / Loot / Bank (Твой контур Контекстного Меню)
            if (gridType == "inventory")
            {
                // TODO: ContextMenuManager.Instance.ShowMenu(mousePos, index, itemId, slot.ContainerEntity);
                Debug.Log($"[UIInputHandler] ПКМ по рюкзаку: предмет {itemId}. Открываем контекстное меню на позиции {mousePos}");
            }
        }
    }
}


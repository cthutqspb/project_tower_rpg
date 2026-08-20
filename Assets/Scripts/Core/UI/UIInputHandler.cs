using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Actions;
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
        private TooltipVisual _tooltipVisual;

        private void Start()
        {
            var actions = UnityEngine.InputSystem.InputSystem.actions;
            _toggleCharacterAction = actions.FindAction("UI/ToggleCharacterWindow");
            _closeWindowAction = actions.FindAction("UI/CloseWindow");

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            UIEvents.OnSlotDoubleClick += HandleSlotDoubleClick;
            UIEvents.OnSlotRightClick += HandleSlotRightClick;

            var panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (panelRenderer != null)
            {
                panelRenderer.RegisterUIReloadCallback((renderer, root, version) => 
                {
                    if (root == null) return;
                    _tooltipVisual = new TooltipVisual();
                    root.Add(_tooltipVisual);
                });
            }
        }

        private void OnDestroy()
        {
            UIEvents.OnSlotDoubleClick -= HandleSlotDoubleClick;
            UIEvents.OnSlotRightClick -= HandleSlotRightClick;
        }

        private void Update()
        {
            if (_toggleCharacterAction.triggered) UIEvents.TriggerToggleCharacterWindow();
            if (_closeWindowAction.triggered) WindowManager.CloseTop();

            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TooltipManager.UpdateMouse(mousePos.x, mousePos.y);
            }

            if (_tooltipVisual != null) _tooltipVisual.UpdateTick();
        }

        // ================================================================
        // 🎯 ДАБЛКЛИК (БЕЗ GRIDTYPE!)
        // ================================================================
        private void HandleSlotDoubleClick(SlotElement slot, int index, string itemId, int amount)
        {
            if (slot.ContainerEntity == Entity.Null) return;

            var em = _entityManager;

            bool isInventory = em.HasComponent<InventoryTag>(slot.ContainerEntity);
            bool isPaperdoll = em.HasComponent<PaperdollTag>(slot.ContainerEntity);
            bool isContainer = em.HasComponent<ContainerTag>(slot.ContainerEntity);

            Entity targetContainerEntity = Entity.Null;

            // КЕЙС 1: ИНВЕНТАРЬ → кукла
            if (isInventory && !isContainer)
            {
                var ownerEntity = em.GetComponentData<ContainerConfigComponent>(slot.ContainerEntity).Owner;

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
                    CreateTransferCommand(em, slot.ContainerEntity, index, targetContainerEntity, itemId, amount);
                    Debug.Log($"[UIInputHandler] Даблклик: экипировка {itemId}");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найдена кукла для {itemId}");
                return;
            }

            // КЕЙС 2: КУКЛА → инвентарь
            if (isPaperdoll)
            {
                var ownerEntity = em.GetComponentData<ContainerConfigComponent>(slot.ContainerEntity).Owner;

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
                    CreateTransferCommand(em, slot.ContainerEntity, index, targetContainerEntity, itemId, amount);
                    Debug.Log($"[UIInputHandler] Даблклик: снятие {itemId}");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найден инвентарь для снятия {itemId}");
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
                    CreateTransferCommand(em, slot.ContainerEntity, index, targetContainerEntity, itemId, amount);
                    Debug.Log($"[UIInputHandler] Даблклик: забрал {itemId} из контейнера");
                    return;
                }

                Debug.LogWarning($"[UIInputHandler] Не найден инвентарь игрока для {itemId}");
                return;
            }

            Debug.LogWarning($"[UIInputHandler] Неизвестный тип контейнера для даблклика: {slot.ContainerEntity}");
        }

        // ================================================================
        // 🎯 ПКМ (БЕЗ GRIDTYPE!)
        // ================================================================
        private void HandleSlotRightClick(SlotElement slot, int index, string itemId, int amount, Vector2 mousePos)
        {
            if (slot.ContainerEntity == Entity.Null) return;

            var em = _entityManager;

            bool isAuraFrame = em.HasComponent<AuraFrameTag>(slot.ContainerEntity);
            bool isActionBar = em.HasComponent<ActionBarTag>(slot.ContainerEntity);
            bool isInventory = em.HasComponent<InventoryTag>(slot.ContainerEntity);
            bool isContainer = em.HasComponent<ContainerTag>(slot.ContainerEntity);

            // КЕЙС 1: АУРА → снять бафф
            if (isAuraFrame)
            {
                var actionEntity = em.CreateEntity();
                em.AddComponentData(actionEntity, new ActionCommand
                {
                    ActionType = "disable_aura",
                    SourceEntity = slot.ContainerEntity,
                    SourceSlot = index,
                    ItemId = itemId
                });
                Debug.Log($"[UIInputHandler] ПКМ по ауре: снятие баффа [{itemId}]");
                return;
            }

            // КЕЙС 2: ACTION BAR → игнорируем
            if (isActionBar)
            {
                return;
            }

            // КЕЙС 3: ИНВЕНТАРЬ (не сундук) → контекстное меню
            if (isInventory && !isContainer)
            {
                Debug.Log($"[UIInputHandler] ПКМ по рюкзаку: {itemId} на {mousePos}");
                return;
            }

            // КЕЙС 4: СУНДУК → осмотр
            if (isContainer)
            {
                Debug.Log($"[UIInputHandler] ПКМ по сундуку: осмотр {itemId}");
                return;
            }

            Debug.Log($"[UIInputHandler] ПКМ по неизвестному контейнеру: {slot.ContainerEntity}");
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

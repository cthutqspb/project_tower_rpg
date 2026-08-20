using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Actions;          // ← ДОБАВЛЕНО
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
            if (_toggleCharacterAction.triggered)
            {
                UIEvents.TriggerToggleCharacterWindow();
            }

            if (_closeWindowAction.triggered)
            {
                WindowManager.CloseTop();
            }

            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TooltipManager.UpdateMouse(mousePos.x, mousePos.y);
            }

            if (_tooltipVisual != null)
            {
                _tooltipVisual.UpdateTick();
            }
        }

        private void HandleSlotDoubleClick(SlotElement slot, int index, string gridType, string itemId, int amount)
        {
            if (slot.ContainerEntity == Entity.Null) return;

            var em = _entityManager;
            var actionEntity = em.CreateEntity();

            var currentConfig = em.GetComponentData<ContainerConfigComponent>(slot.ContainerEntity);
            Entity unitOwner = currentConfig.Owner;

            Entity targetContainerEntity = Entity.Null;

            var containerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ContainerConfigComponent>());
            var allContainers = containerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var container in allContainers)
            {
                var cfg = em.GetComponentData<ContainerConfigComponent>(container);
                if (cfg.Owner == unitOwner)
                {
                    if (gridType == "inventory" && cfg.Rows == 1)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                    if (gridType == "paperdoll" && cfg.Rows > 1)
                    {
                        targetContainerEntity = container;
                        break;
                    }
                }
            }
            allContainers.Dispose();

            if (targetContainerEntity == Entity.Null)
            {
                Debug.LogWarning($"[UIInputHandler] Парный контейнер для юнита {unitOwner} не найден!");
                return;
            }

            if (gridType == "inventory")
            {
                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "item_transfer",
                    SourceEntity = slot.ContainerEntity,
                    SourceSlot = index,
                    TargetEntity = targetContainerEntity,
                    TargetSlot = -1,
                    ItemId = itemId,
                    Amount = amount
                });
                Debug.Log($"[UIInputHandler] Даблклик: экипировка {itemId}");
            }
            else if (gridType == "paperdoll")
            {
                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "item_transfer",
                    SourceEntity = slot.ContainerEntity,
                    SourceSlot = index,
                    TargetEntity = targetContainerEntity,
                    TargetSlot = -1,
                    ItemId = itemId,
                    Amount = amount
                });
                Debug.Log($"[UIInputHandler] Даблклик: снятие {itemId}");
            }
        }

        private void HandleSlotRightClick(SlotElement slot, int index, string gridType, string itemId, int amount, Vector2 mousePos)
        {
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
                Debug.Log($"[UIInputHandler] ПКМ по ауре: снятие баффа [{itemId}]");
                return;
            }

            if (gridType == "action_bar")
            {
                return;
            }

            if (gridType == "inventory")
            {
                Debug.Log($"[UIInputHandler] ПКМ по рюкзаку: {itemId} на {mousePos}");
            }
        }
    }
}

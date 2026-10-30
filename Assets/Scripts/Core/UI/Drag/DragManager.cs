using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.Core.Colors;
using ProjectTowerRpg.Core.Utils;

namespace ProjectTowerRpg.Core.UI
{
    public class DragManager : MonoBehaviour
    {
        private static DragManager _instance;
        public static DragManager Instance => _instance;

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private IPanel _panel;
        private Label _ghost;
        private Label _amountLabel;
        private DragData _activeDrag;
        private EntityManager _entityManager;
        
        private void Awake() => _instance = this;

        private void Start()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError("[DragManager]: PanelRenderer не найден!");
                return;
            }

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement root, int version)
        {
            _root = root;
            _panel = root?.panel;
            if (_root == null) return;

            _ghost = new Label();
            _ghost.name = "drag-ghost";
            _ghost.style.width = 48;
            _ghost.style.height = 48;
            _ghost.style.fontSize = 39;
            _ghost.style.display = DisplayStyle.None;
            _ghost.style.backgroundColor = SolarizedOsakaNight.Surface.SetAlpha(0.85f);
            _ghost.pickingMode = PickingMode.Ignore;

            _amountLabel = new Label();
            _amountLabel.style.position = Position.Absolute;
            _amountLabel.style.bottom = 2;
            _amountLabel.style.right = 4;
            _amountLabel.style.fontSize = 14;
            _amountLabel.style.color = SolarizedOsakaNight.Text;
            _amountLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _amountLabel.pickingMode = PickingMode.Ignore;
            _ghost.Add(_amountLabel);

            _root.Add(_ghost);
            Debug.Log("[DragManager]: Ghost создан");
        }
        
        private void Update()
        {
            if (_ghost == null || _activeDrag == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePosition = mouse.position.ReadValue();
            _ghost.style.left = mousePosition.x;
            _ghost.style.top = Screen.height - mousePosition.y;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                var panelPos = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
                var picked = _panel?.Pick(panelPos);
                
                if (picked == _ghost) picked = null;

                bool isOverWorld = picked == null || picked == _root;

                if (isOverWorld)
                {
                    HandleWorldDrop();
                    return;
                }

                // 🎯 Ищем IDropTarget вверх по дереву
                IDropTarget dropTarget = picked as IDropTarget ?? picked.GetFirstAncestorOfType<IDropTarget>();

                if (dropTarget != null)
                {
                    if (dropTarget.CanAcceptDrop(_activeDrag))
                    {
                        // Если это слот, нам всё ещё нужен его SlotIndex и ContainerEntity.
                        // Если dropTarget — это SlotElement, приводим тип (или добавляем свойства в интерфейс).
                        if (dropTarget is SlotElement slotElement)
                        {
                            Finish(slotElement, slotElement.SlotIndex);
                        }
                        else if (dropTarget is IEntityContainer container)
                        {
                            Finish(container, -1);
                        }
                        return;
                    }
                    else
                    {
                        // 🎯 UI есть, но он не принимает этот дроп (например, аура)
                        CancelDrag();
                        return;
                    }
                }

                // UI есть, но это не DropTarget (например, чатлог) — отменяем
                CancelDrag();
            }
        }

        private void HandleWorldDrop()
        {
            if (!PlayerUtils.TryGetPosition(out float3 playerPosition))
            {
                CancelDrag();
                return;
            }

            Entity sourceEntity = _activeDrag.SourceEntity;
            int sourceSlot = _activeDrag.SlotIndex;

            if (sourceEntity == Entity.Null)
            {
                Debug.LogWarning("[DragManager] Исходная ECS-сущность не найдена");
                CancelDrag();
                return;
            }

            ClearGhost();
            var em = _entityManager;
            var actionEntity = em.CreateEntity();

            bool isFromActionBar = em.HasComponent<PlayerTag>(sourceEntity);

            if (isFromActionBar)
            {
                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Action = PlayerActions.ActionBarAssign,
                    SourceEntity = sourceEntity,
                    SourceSlot = sourceSlot,
                    TargetEntity = sourceEntity,
                    TargetSlot = -1,
                    ItemId = _activeDrag.ItemId,
                    Amount = _activeDrag.Amount
                });

                Debug.Log($"[DragManager]: Ярлык экшен-бара '{_activeDrag.ItemId}' выброшен в мир из слота #{sourceSlot}.");
            }
            else
            {
                float3 dropPosition = PositionUtils.GetDropPosition(playerPosition);

                int actualAmount = GetActualAmount(em, sourceEntity, sourceSlot);
                bool isSplit = _activeDrag.Amount < actualAmount;

                if (isSplit)
                {
                    em.AddComponentData(actionEntity, new ActionCommand
                    {
                        Action = ItemActions.Split,
                        SourceEntity = sourceEntity,
                        SourceSlot = sourceSlot,
                        TargetEntity = Entity.Null,
                        TargetSlot = -1,
                        ItemId = _activeDrag.ItemId,
                        Amount = _activeDrag.Amount,
                        Position = dropPosition
                    });

                    Debug.Log($"[DragManager]: Split {_activeDrag.ItemId} x{_activeDrag.Amount}/{actualAmount} на землю: {dropPosition}");
                }
                else
                {
                    em.AddComponentData(actionEntity, new ActionCommand
                    {
                        Action = ItemActions.Drop,
                        SourceEntity = sourceEntity,
                        SourceSlot = sourceSlot,
                        TargetEntity = Entity.Null,
                        TargetSlot = -1,
                        ItemId = _activeDrag.ItemId,
                        Amount = _activeDrag.Amount,
                        Position = dropPosition
                    });

                    Debug.Log($"[DragManager]: Дроп {_activeDrag.ItemId} на землю: {dropPosition}");
                }
            }

            _activeDrag = null;
        }

        public void StartDrag(DragData data, bool captureOnRoot = false)
        {
            if (_activeDrag != null) CancelDrag();

            _activeDrag = data;

            _ghost.BringToFront();
            _ghost.style.position = Position.Absolute;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                _ghost.style.left = mousePosition.x;
                _ghost.style.top = Screen.height - mousePosition.y;
            }

            foreach (var className in data.Icon.Classes)
                _ghost.AddToClassList(className);

            _ghost.text = data.Icon.Glyph;

            _ghost.style.display = DisplayStyle.Flex;
            _ghost.style.opacity = 1;

            _amountLabel.text = data.Amount > 1 ? data.Amount.ToString() : "";
            _amountLabel.style.display = data.Amount > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            // 🎯 Для сплита: захватываем пойнтер на корне, чтобы PointerDown/Move/Up
            // не уходили в слоты-цели и не перебивали активный драг.
            if (captureOnRoot && _root != null && !_root.HasPointerCapture(PointerId.mousePointerId))
                _root.CapturePointer(PointerId.mousePointerId);

            UIEvents.TriggerDragStarted();
            Debug.Log($"[DragManager]: Драг начат {data.ItemId} x{data.Amount}");
        }

        public void Finish(object targetComponent, int targetSlot)
        {
            if (_activeDrag == null) return;

            ClearGhost();

            if (targetComponent is SlotElement targetSlotElement)
            {
                Entity sourceEntity = _activeDrag.SourceEntity;
                int sourceSlot = _activeDrag.SlotIndex;
                Entity targetEntity = targetSlotElement.ContainerEntity;

                if (sourceEntity == Entity.Null || targetEntity == Entity.Null)
                {
                    _activeDrag = null;
                    return;
                }

                var em = _entityManager;
                var actionEntity = em.CreateEntity();

                bool isTargetBar = em.HasComponent<PlayerTag>(targetEntity);

                // Защита: нельзя тащить вещи из чужих сундуков/трупов прямо на панель
                if (isTargetBar && em.HasComponent<ContainerTag>(sourceEntity))
                {
                    Debug.LogWarning("❌ ГЕЙМДИЗАЙН: Нельзя тащить вещи из чужих сундуков или трупов сразу на панель!");
                    _activeDrag = null;
                    return;
                }

                // Определяем экшен
                ActionKind action;
                int finalTargetSlot;

                if (isTargetBar)
                {
                    action = PlayerActions.ActionBarAssign;
                    finalTargetSlot = targetSlotElement.SlotIndex;
                }
                else
                {
                    int actualAmount = GetActualAmount(em, sourceEntity, sourceSlot);
                    bool isSplit = _activeDrag.Amount < actualAmount;
                    action = isSplit ? ItemActions.Split : ItemActions.Transfer;
                    finalTargetSlot = targetSlot;
                }

                em.AddComponentData(actionEntity, new ActionCommand
                {
                    Action = action,
                    SourceEntity = sourceEntity,
                    SourceSlot = sourceSlot,
                    TargetEntity = targetEntity,
                    TargetSlot = finalTargetSlot,
                    ItemId = _activeDrag.ItemId,
                    Amount = _activeDrag.Amount
                });

                Debug.Log($"[DragManager]: Спавн ECS-команды {action} -> Слот #{finalTargetSlot}, Amount={_activeDrag.Amount}");
            }

            _activeDrag = null;
        }

        // 🎯 Хелпер: сколько предметов реально лежит в исходном слоте
        private int GetActualAmount(EntityManager em, Entity sourceEntity, int sourceSlot)
        {
            if (!em.HasBuffer<ItemSlot>(sourceEntity)) return 0;

            var slots = em.GetBuffer<ItemSlot>(sourceEntity);
            if (sourceSlot < 0 || sourceSlot >= slots.Length) return 0;

            return slots[sourceSlot].Amount;
        }

        public void CancelDrag()
        {
            if (_activeDrag == null) return;

            ClearGhost();
            _activeDrag = null;
            Debug.Log("[DragManager] Драг отменён");
        }

        private void ClearGhost()
        {
            if (_ghost == null) return;

            // 🎯 ОБЯЗАТЕЛЬНО: отпускаем захват с корня, иначе после первого сплита
            // root залипает владельцем пойнтера и больше никакой ввод не доходит до UI.
            if (_root != null && _root.HasPointerCapture(PointerId.mousePointerId))
                _root.ReleasePointer(PointerId.mousePointerId);

            _ghost.style.display = DisplayStyle.None;
            _ghost.text = "";
            _amountLabel.text = "";
            _amountLabel.style.display = DisplayStyle.None;

            UIEvents.TriggerDragEnded();
        }

        public bool IsDragging => _activeDrag != null;
        public DragData GetActiveDrag() => _activeDrag;

        private void OnDestroy()
        {
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);

            _instance = null;
        }
    }
}

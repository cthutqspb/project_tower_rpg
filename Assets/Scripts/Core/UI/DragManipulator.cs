using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    public enum DragMode
    {
        UIElement,   // Перемещение любого UI-элемента (окно, панель, хпбар)
        Slot         // Драг из слота (предмет, способность)
    }

    public class DragManipulator : PointerManipulator
    {
        private DragMode _mode;
        private VisualElement _targetElement;      // Что двигаем (окно или слот)
        private VisualElement _dragElement;        // На чём висит драг (хедер)
        private VisualElement _ghost;              // Призрак для Slot режима
        private Label _amountLabel;                // Количество для Slot
        
        // Для UIElement режима
        private Vector2 _startPosition;
        private Vector3 _pointerStartPosition;
        
        // Для Slot режима
        private object _dragData;
        private string _sourceId;
        private int _slotIndex;
        private string _itemId;
        private int _amount;

        // Конструктор для UIElement режима
        public DragManipulator(VisualElement dragElement, VisualElement targetElement, DragMode mode)
        {
            _dragElement = dragElement;
            _targetElement = targetElement;
            _mode = mode;
            this.target = dragElement;
        }

        // Конструктор для Slot режима
        public DragManipulator(VisualElement target, DragMode mode, 
                               object dragData = null, string sourceId = null, 
                               int slotIndex = -1, string itemId = null, int amount = 1)
        {
            this.target = target;
            _dragElement = target;
            _targetElement = target;
            _mode = mode;
            _dragData = dragData;
            _sourceId = sourceId;
            _slotIndex = slotIndex;
            _itemId = itemId;
            _amount = amount;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        // ================================================================
        // UIElement MODE
        // ================================================================
        private void StartUIElementDrag(PointerDownEvent evt)
        {
            _startPosition = _targetElement.transform.position;
            _pointerStartPosition = evt.position;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void UpdateUIElementDrag(PointerMoveEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                Vector3 delta = evt.position - _pointerStartPosition;
                _targetElement.transform.position = _startPosition + (Vector2)delta;
                evt.StopPropagation();
            }
        }

        private void EndUIElementDrag(PointerUpEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        // ================================================================
        // Slot MODE
        // ================================================================
        private void StartSlotDrag(PointerDownEvent evt)
        {
            if (_dragData == null) return;

            CreateGhost();
            
            DragManager.Instance.StartDrag(
                source: target,
                slotIndex: _slotIndex,
                itemId: _itemId,
                amount: _amount,
                icon: null,
                sourceId: _sourceId,
                gridType: "inventory"
            );

            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void CreateGhost()
        {
            var root = target.parent;
            if (root == null) return;

            _ghost = new VisualElement();
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = 48;
            _ghost.style.height = 48;
            _ghost.style.backgroundColor = new Color(1, 1, 1, 0.9f);
            _ghost.style.borderTopWidth = 2;
            _ghost.style.borderBottomWidth = 2;
            _ghost.style.borderLeftWidth = 2;
            _ghost.style.borderRightWidth = 2;
            _ghost.style.borderTopColor = Color.white;
            _ghost.style.borderBottomColor = Color.white;
            _ghost.style.borderLeftColor = Color.white;
            _ghost.style.borderRightColor = Color.white;
            _ghost.style.display = DisplayStyle.None;
            _ghost.pickingMode = PickingMode.Ignore;

            _amountLabel = new Label();
            _amountLabel.style.position = Position.Absolute;
            _amountLabel.style.bottom = 2;
            _amountLabel.style.right = 4;
            _amountLabel.style.fontSize = 14;
            _amountLabel.style.color = Color.white;
            _amountLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _ghost.Add(_amountLabel);

            root.Add(_ghost);
        }

        private void UpdateSlotDrag(PointerMoveEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId) || _ghost == null) return;

            Vector2 mousePos = evt.position;
            _ghost.style.left = mousePos.x - 24;
            _ghost.style.top = mousePos.y - 24;
            _ghost.style.display = DisplayStyle.Flex;

            evt.StopPropagation();
        }

        private void EndSlotDrag(PointerUpEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId)) return;

            target.ReleasePointer(evt.pointerId);
            
            if (_ghost != null)
            {
                _ghost.style.display = DisplayStyle.None;
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }

            if (DragManager.Instance.IsDragging)
            {
                var picked = target.panel.Pick(evt.position);
                
                if (picked != null)
                {
                    var provider = picked.GetFirstAncestorOfType<IDataSourceProvider>();
                    if (provider != null)
                    {
                        DragManager.Instance.Finish(provider, -1);
                        evt.StopPropagation();
                        return;
                    }
                }
                
                DragManager.Instance.Finish(null, -1);
            }

            evt.StopPropagation();
        }

        // ================================================================
        // ОБЩИЕ ОБРАБОТЧИКИ
        // ================================================================

        private void OnPointerDown(PointerDownEvent evt)
        {
            switch (_mode)
            {
                case DragMode.UIElement:
                    StartUIElementDrag(evt);
                    break;
                case DragMode.Slot:
                    StartSlotDrag(evt);
                    break;
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            switch (_mode)
            {
                case DragMode.UIElement:
                    UpdateUIElementDrag(evt);
                    break;
                case DragMode.Slot:
                    UpdateSlotDrag(evt);
                    break;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            switch (_mode)
            {
                case DragMode.UIElement:
                    EndUIElementDrag(evt);
                    break;
                case DragMode.Slot:
                    EndSlotDrag(evt);
                    break;
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
            }
            
            if (_mode == DragMode.Slot && _ghost != null)
            {
                _ghost.style.display = DisplayStyle.None;
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }
        }
    }
}

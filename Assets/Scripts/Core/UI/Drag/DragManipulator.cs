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
        
        // Для UIElement режима
        private Vector2 _startPosition;
        private Vector3 _pointerStartPosition;

        // Конструктор для UIElement режима (драг хедера)
        public DragManipulator(VisualElement dragElement, VisualElement targetElement, DragMode mode)
        {
            _dragElement = dragElement;
            _targetElement = targetElement;
            _mode = mode;
            this.target = dragElement;
        }

        // Конструктор для Slot режима
        public DragManipulator(VisualElement target, DragMode mode)
        {
            this.target = target;
            _dragElement = target;
            _targetElement = target;
            _mode = mode;
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
        // UIElement MODE (полностью здесь)
        // ================================================================
        private void StartUIElementDrag(PointerDownEvent evt)
        {
            var translate = _targetElement.resolvedStyle.translate;
            _startPosition = new Vector2(translate.x, translate.y);
            _pointerStartPosition = evt.position;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void UpdateUIElementDrag(PointerMoveEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                Vector3 delta = evt.position - _pointerStartPosition;
                _targetElement.style.translate = new Translate(
                    _startPosition.x + delta.x,
                    _startPosition.y + delta.y,
                    0
                );
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
        // Slot MODE (только старт, остальное в DragManager)
        // ================================================================
        private void StartSlotDrag(PointerDownEvent evt)
        {
            if (target is not IDragSource dragSource || !dragSource.CanDrag()) return;

            var data = dragSource.GetDragData();
            if (data == null) return;

            DragManager.Instance.StartDrag(data);

            target.CapturePointer(evt.pointerId);
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
                    // Всё движение обрабатывает DragManager
                    evt.StopPropagation();
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
                    // Освобождаем захват, завершение в DragManager
                    if (target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }
                    evt.StopPropagation();
                    break;
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
            }
            
            if (_mode == DragMode.Slot)
            {
                DragManager.Instance.CancelDrag();
            }
        }
    }
}

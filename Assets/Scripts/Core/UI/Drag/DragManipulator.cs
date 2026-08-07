using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    // Если enum DragMode объявлен в другом файле в этом же namespace, эту строку можно удалить.
    // Если он в другом namespace — добавьте нужный using наверху.
    public enum DragMode
    {
        UIElement,   
        Slot         
    }

    public class DragManipulator : PointerManipulator
    {
        private DragMode _mode;
        private VisualElement _targetElement;      // Что двигаем (окно или слот)
        private VisualElement _dragElement;        // На чём висит драг (хедер)
        
        // Храним точку хвата относительно левого верхнего угла элемента
        private Vector2 _pointerOffset;

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
        // UIElement MODE (Исправлено под ресайз и Hyprland)
        // ================================================================
        private void StartUIElementDrag(PointerDownEvent evt)
        {
            // Переключаем в абсолют при клике, чтобы Flexbox не блокировал оси при Maximize окна
            if (_targetElement.style.position != Position.Absolute)
            {
                _targetElement.style.position = Position.Absolute;
                _targetElement.style.left = _targetElement.layout.x;
                _targetElement.style.top = _targetElement.layout.y;
                _targetElement.style.translate = StyleKeyword.Null; // Сбрасываем старый транслейт
            }

            // Запоминаем смещение курсора внутри окна
            _pointerOffset = _targetElement.WorldToLocal(evt.position);
            
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void UpdateUIElementDrag(PointerMoveEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                VisualElement root = _targetElement.panel.visualTree;

                // evt.position — это экранные координаты. Считаем левый верхний угол окна
                float targetX = evt.position.x - _pointerOffset.x;
                float targetY = evt.position.y - _pointerOffset.y;

                // Клэмпим по живым актуальным размерам root экрана
                _targetElement.style.left = Mathf.Clamp(targetX, 0f, root.layout.width - _targetElement.layout.width);
                _targetElement.style.top = Mathf.Clamp(targetY, 0f, root.layout.height - _targetElement.layout.height);

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
        // Slot MODE (Оставляем без изменений, как в вашем исходнике)
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


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
        
        // Для UIElement режима: храним смещение курсора относительно ЛОКАЛЬНЫХ координат target-элемента
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
        // UIElement MODE (полностью здесь)
        // ================================================================
        private void StartUIElementDrag(PointerDownEvent evt)
        {
            // 1. Переводим элемент на абсолютное позиционирование, чтобы верстка (Flexbox) его не держала
            if (_targetElement.style.position != Position.Absolute)
            {
                // Запоминаем текущее положение на экране перед переключением
                float currentLeft = _targetElement.layout.x;
                float currentTop = _targetElement.layout.y;

                _targetElement.style.position = Position.Absolute;
                _targetElement.style.left = currentLeft;
                _targetElement.style.top = currentTop;
                
                // Сбрасываем translate, так как теперь управляем через left/top
                _targetElement.style.translate = StyleKeyword.Null; 
            }

            // 2. Запоминаем точку хвата курсора относительно самого элемента
            _pointerOffset = _targetElement.WorldToLocal(evt.position);

            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void UpdateUIElementDrag(PointerMoveEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                VisualElement root = _targetElement.panel.visualTree;
                VisualElement parent = _targetElement.parent ?? root;

                // 1. Актуальные размеры в текущий кадр
                float screenWidth = root.layout.width;
                float screenHeight = root.layout.height;
                float elementWidth = _targetElement.layout.width;
                float elementHeight = _targetElement.layout.height;

                // 2. Позиция мыши в пространстве родителя
                Vector2 mouseInParentSpace = parent.WorldToLocal(evt.position);

                // 3. Желаемая позиция левого верхнего угла элемента
                float targetX = mouseInParentSpace.x - _pointerOffset.x;
                float targetY = mouseInParentSpace.y - _pointerOffset.y;

                // 4. Перевод в мировые координаты для честного Clamp по границам экрана
                Vector2 targetInWorld = parent.LocalToWorld(new Vector2(targetX, targetY));

                // 5. Ограничиваем строго рамками экрана (от 0 до краев)
                float clampedWorldX = Mathf.Clamp(targetInWorld.x, 0f, screenWidth - elementWidth);
                float clampedWorldY = Mathf.Clamp(targetInWorld.y, 0f, screenHeight - elementHeight);

                // 6. Возвращаем ограниченные координаты обратно в родительский контейнер
                Vector2 finalLocalPos = parent.WorldToLocal(new Vector2(clampedWorldX, clampedWorldY));

                // 7. Напрямую задаем left и top. Никакие флексы, леяуты и ресайзы больше не заблокируют ось X!
                _targetElement.style.left = finalLocalPos.x;
                _targetElement.style.top = finalLocalPos.y;
                
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


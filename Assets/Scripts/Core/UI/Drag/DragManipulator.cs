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

        // Настройки трешхолда
        private const float DragThreshold = 5f;    // Порог в пикселях
        private bool _isDragStarted;               // Флаг активного драга
        private Vector2 _startPointerPosition;     // Точка первого нажатия

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
        // UIElement MODE
        // ================================================================
        private void StartUIElementDrag(Vector2 mousePosition, int pointerId)
        {
            if (_targetElement.style.position != Position.Absolute)
            {
                float currentLeft = _targetElement.layout.x;
                float currentTop = _targetElement.layout.y;

                _targetElement.style.position = Position.Absolute;
                _targetElement.style.left = currentLeft;
                _targetElement.style.top = currentTop;
                
                _targetElement.style.translate = StyleKeyword.Null; 
            }

            // Считаем офсет от ИСХОДНОЙ точки клика, чтобы окно не прыгало при прохождении порога
            _pointerOffset = _targetElement.WorldToLocal(mousePosition);

            target.CapturePointer(pointerId);
        }

        private void UpdateUIElementDrag(PointerMoveEvent evt)
        {
            if (target.HasPointerCapture(evt.pointerId))
            {
                VisualElement root = _targetElement.panel.visualTree;
                VisualElement parent = _targetElement.parent ?? root;

                float screenWidth = root.layout.width;
                float screenHeight = root.layout.height;
                float elementWidth = _targetElement.layout.width;
                float elementHeight = _targetElement.layout.height;

                Vector2 mouseInParentSpace = parent.WorldToLocal(evt.position);

                float targetX = mouseInParentSpace.x - _pointerOffset.x;
                float targetY = mouseInParentSpace.y - _pointerOffset.y;

                Vector2 targetInWorld = parent.LocalToWorld(new Vector2(targetX, targetY));

                float clampedWorldX = Mathf.Clamp(targetInWorld.x, 0f, screenWidth - elementWidth);
                float clampedWorldY = Mathf.Clamp(targetInWorld.y, 0f, screenHeight - elementHeight);

                Vector2 finalLocalPos = parent.WorldToLocal(new Vector2(clampedWorldX, clampedWorldY));

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
        // Slot MODE
        // ================================================================
        private void StartSlotDrag(int pointerId)
        {
            if (target is not IDragSource dragSource || !dragSource.CanDrag())
            {
                target.ReleasePointer(pointerId);
                _isDragStarted = false;
                return;
            }

            var data = dragSource.GetDragData();
            if (data == null)
            {
                target.ReleasePointer(pointerId);
                _isDragStarted = false;
                return;
            }

            DragManager.Instance.StartDrag(data);
            target.CapturePointer(pointerId);
        }

        // ================================================================
        // ОБЩИЕ ОБРАБОТЧИКИ
        // ================================================================
        private void OnPointerDown(PointerDownEvent evt)
        {   
            if (evt.button != 0) return;

            _isDragStarted = false;
            _startPointerPosition = evt.position;

            // Захватываем поинтер сразу, чтобы гарантированно поймать PointerMoveEvent
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId)) return;

            if (!_isDragStarted)
            {
                float distance = Vector2.Distance(_startPointerPosition, evt.position);
                if (distance < DragThreshold) return;

                // Пересекли порог — фиксируем старт
                _isDragStarted = true;

                switch (_mode)
                {
                    case DragMode.UIElement:
                        StartUIElementDrag(_startPointerPosition, evt.pointerId);
                        break;
                    case DragMode.Slot:
                        StartSlotDrag(evt.pointerId);
                        break;
                }
            }

            if (_isDragStarted)
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
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0) return;

            // Если отпустили кнопку до прохождения порога — сбрасываем захват (это был просто клик)
            if (!_isDragStarted && target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

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
            
            if (_mode == DragMode.Slot && _isDragStarted)
            {
                DragManager.Instance.CancelDrag();
            }

            _isDragStarted = false;
        }
    }
}


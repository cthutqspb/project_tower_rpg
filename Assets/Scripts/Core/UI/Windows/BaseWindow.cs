using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI.Windows
{
    // Базовое окно - ТОЛЬКО логика окна (создание, драг, показ/скрытие)
    // Блокировка инпута теперь в UIManager на уровне всего UI
    public abstract class BaseWindow : MonoBehaviour
    {
        [Header("Настройки UI Toolkit")]
        [SerializeField] private VisualTreeAsset windowUxml;

        public VisualElement RootNode { get; private set; }
        protected VisualElement HeaderNode { get; private set; }

        public Action OnWindowShown;
        public Action OnWindowHidden;

        private PanelRenderer _globalPanelRenderer;
        private bool _isDragging = false;

        public bool IsVisible => RootNode != null && RootNode.style.display == DisplayStyle.Flex;

        protected virtual void Start()
        {
            if (windowUxml == null)
            {
                Debug.LogError($"🚨 [{gameObject.name}]: Забыл закинуть UXML ассет в инспектор!");
                return;
            }

            _globalPanelRenderer = FindAnyObjectByType<PanelRenderer>();
            
            if (_globalPanelRenderer == null)
            {
                Debug.LogError($"🚨 [{gameObject.name}]: На сцене нет пустышки [UI] с компонентом PanelRenderer!");
                return;
            }

            _globalPanelRenderer.RegisterUIReloadCallback(OnGlobalUiReloaded);
        }

        protected virtual void OnDestroy()
        {
            if (_globalPanelRenderer != null)
            {
                _globalPanelRenderer.UnregisterUIReloadCallback(OnGlobalUiReloaded);
            }
        }

        private void OnGlobalUiReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || RootNode != null) return;

            // Клонируем разметку окна
            RootNode = windowUxml.CloneTree();
            RootNode.pickingMode = PickingMode.Position;
            globalUiRoot.Add(RootNode);

            // =========================================================================
            // ТОЛЬКО ЛОГИКА ОКНА (без блокировки - она в UIManager)
            // =========================================================================
            
            // Находим хедер для драга
            HeaderNode = RootNode.Q<VisualElement>(className: "window-header-label") 
                         ?? RootNode.Q<VisualElement>("header");

            if (HeaderNode != null)
            {
                HeaderNode.RegisterCallback<PointerDownEvent>(OnDragStart);
                HeaderNode.RegisterCallback<PointerMoveEvent>(OnDragMove);
                HeaderNode.RegisterCallback<PointerUpEvent>(OnDragEnd);
            }

            // Передаем управление в дочерний класс
            OnWindowTreeRebuilt(RootNode);
        }

        protected abstract void OnWindowTreeRebuilt(VisualElement panelRoot);

        public virtual void Show()
        {
            if (RootNode == null) return;
            RootNode.style.display = DisplayStyle.Flex;
            OnWindowShown?.Invoke();
        }

        public virtual void Hide()
        {
            if (RootNode == null) return;
            RootNode.style.display = DisplayStyle.None;
            OnWindowHidden?.Invoke();
        }

        public void BringToFront()
        {
            RootNode?.BringToFront();
        }

        // =========================================================================
        // ЛОГИКА ДРАГА
        // =========================================================================

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!_isDragging || RootNode == null || RootNode.childCount == 0) return;

            VisualElement windowBox = RootNode[0];

            if (windowBox != null)
            {
                if (windowBox.style.position != Position.Absolute)
                {
                    windowBox.style.position = Position.Absolute;
                    windowBox.style.left = windowBox.resolvedStyle.left;
                    windowBox.style.top = windowBox.resolvedStyle.top;
                }

                windowBox.style.left = windowBox.style.left.value.value + evt.deltaPosition.x;
                windowBox.style.top = windowBox.style.top.value.value + evt.deltaPosition.y;
            }
            
            evt.StopPropagation();
        }

        private void OnDragStart(PointerDownEvent evt)
        {
            _isDragging = true;
            HeaderNode.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            HeaderNode.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }
}

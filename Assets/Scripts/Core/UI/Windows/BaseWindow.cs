using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI.Windows
{
    // 👑 ТИТАНОВЫЙ КОНТРАКТ БАЗОВОГО ОКНА (Версия без слепоты стилей!)
    [RequireComponent(typeof(PanelRenderer))]
    public abstract class BaseWindow : MonoBehaviour
    {
        protected PanelRenderer PanelRenderer { get; private set; }
        public VisualTreeAsset visualTreeAsset => PanelRenderer != null ? PanelRenderer.visualTreeAsset : null;
        public VisualElement RootNode { get; private set; }
        protected VisualElement HeaderNode { get; private set; }

        public Action OnWindowShown;
        public Action OnWindowHidden;

        private bool _isDragging = false;
        private Vector2 _dragStartOffset;

        // 🎰 ОКНО ВИДИМО, ЕСЛИ САМ КОМПОНЕНТ PANEL RENDERER ВКЛЮЧЕН В ИНСПЕКТОРЕ!
        public bool IsVisible => PanelRenderer != null && PanelRenderer.enabled;

        protected virtual void Awake()
        {
            PanelRenderer = GetComponent<PanelRenderer>();
            
            if (PanelRenderer != null)
            {
                // Подписываемся на реактивный Hot Reload коллбэк Unity 6
                PanelRenderer.RegisterUIReloadCallback(InitializeWindowTree);
            }
        }

        protected virtual void OnDestroy()
        {
            if (PanelRenderer != null)
            {
                PanelRenderer.UnregisterUIReloadCallback(InitializeWindowTree);
            }
        }

        private void InitializeWindowTree(PanelRenderer renderer, VisualElement panelRoot, int version)
        {
            if (panelRoot == null) return;

            RootNode = panelRoot;

            HeaderNode = panelRoot.Q<VisualElement>(className: "window-header-label") 
                         ?? panelRoot.Q<VisualElement>("header");

            if (HeaderNode != null)
            {
                HeaderNode.RegisterCallback<PointerDownEvent>(OnDragStart);
                HeaderNode.RegisterCallback<PointerMoveEvent>(OnDragMove);
                HeaderNode.RegisterCallback<PointerUpEvent>(OnDragEnd);
            }

            // Пинаем дочерний скрипт рюкзака через абстрактный хук
            OnWindowTreeRebuilt(panelRoot);
            
            // 🪓 ВЫЖИГАЕМ СЛЕПОТУ СТАРТА: Пусть окно горит при запуске, пока мы дебажим!
            // Hide(); 
        }

        protected abstract void OnWindowTreeRebuilt(VisualElement panelRoot);

        // =========================================================================
        // 🦾 УПРАВЛЕНИЕ ВИДИМОСТЬЮ ЧЕРЕЗ КAНOНИЧНЫЙ ДВИЖКОВЫЙ КОМПОНЕНТ:
        // =========================================================================
        public virtual void Show()
        {
            if (PanelRenderer == null) return;
            
            // Включаем сам 3D-принтер интерфейса в инспекторе!
            PanelRenderer.enabled = true;
            OnWindowShown?.Invoke();
        }

        public virtual void Hide()
        {
            if (PanelRenderer == null) return;
            
            // Гасим сам 3D-принтер интерфейса в инспекторе!
            PanelRenderer.enabled = false;
            OnWindowHidden?.Invoke();
        }

        public virtual void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        // =========================================================================
        // ЛОГИКА НАТИВНОГО ДРАГА (Захват мыши Wayland-эпохи)
        // =========================================================================
        private void OnDragStart(PointerDownEvent evt)
        {
            _isDragging = true;
            _dragStartOffset = evt.localPosition;
            HeaderNode.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!_isDragging || RootNode == null) return;

            Vector2 mousePos = evt.position;
            
            // Ищем само тело окна внутри холста, чтобы двигать именно его, а не весь экран!
            var windowBox = RootNode.Q<VisualElement>(className: "character-window");
            if (windowBox != null)
            {
                windowBox.style.position = Position.Absolute;
                windowBox.style.left = mousePos.x - _dragStartOffset.x;
                windowBox.style.top = mousePos.y - _dragStartOffset.y;
            }
            
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


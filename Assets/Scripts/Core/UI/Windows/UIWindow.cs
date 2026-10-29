using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI
{
    public abstract class UIWindow : MonoBehaviour
    {
        [SerializeField] protected VisualTreeAsset _windowUxml;
        
        protected VisualElement _root;
        protected VisualElement _window;
        protected WindowContext _context;

        private readonly List<IEcsUiBufferReceiver<ItemSlot>> _cachedReceivers = new();

        // 🎯 Сессионный кэш позиций (в памяти, не в сейве).
        // Живёт до выхода из игры. Можно очистить через ResetSavedPositions().
        private static readonly Dictionary<WindowType, Vector2> _savedPositions = new();

        public VisualElement Root => _root;
        public bool IsOpen => _context != null && _context.IsVisible;

        public abstract WindowType Type { get; }
        public virtual Entity BoundEntity => Entity.Null;
        public virtual bool CanCloseByEscape => true;
        
        [HideInInspector] public bool IsInPool;

        public void InitializeWindow(VisualElement globalUiRoot)
        {
            if (globalUiRoot == null || _root != null) return;

            Debug.Log($"🏗️ [{GetType().Name}.InitializeWindow]: Собираем окно...");

            _root = _windowUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.width = new Length(100, LengthUnit.Percent);
            _root.style.height = new Length(100, LengthUnit.Percent);
            _root.pickingMode = PickingMode.Ignore; 
            _root.style.display = DisplayStyle.None;

            if (_root.childCount > 0)
                _window = _root[0];
            else
                Debug.LogError($"[{GetType().Name}] UXML не содержит элементов!");

            globalUiRoot.Add(_root);

            _context = new WindowContext(
                _root,
                onClose: OnInternalWindowClosed,
                onShow: OnInternalWindowShown,
                owner: this
            );

            OnWindowBuilt(_root);
            ScanAndCacheReceivers();
        }

        protected virtual void OnDestroy()
        {
            if (_context != null) WindowManager.Pop(_context);
            if (_root != null && _root.parent != null) _root.RemoveFromHierarchy();
        }

        protected void ScanAndCacheReceivers()
        {
            if (_root == null) return;
            if (IsOpen) OnInternalWindowClosed();

            _cachedReceivers.Clear();
            _root.Query<VisualElement>().ForEach(element =>
            {
                if (element is IEcsUiBufferReceiver<ItemSlot> receiver) _cachedReceivers.Add(receiver);
            });

            if (IsOpen) OnInternalWindowShown();
        }

        private void OnInternalWindowShown()
        {
            foreach (var receiver in _cachedReceivers)
            {
                if (receiver.BoundEntity != Entity.Null)
                {
                    UIRegistry.Register(receiver.BoundEntity, receiver);
                    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                    if (em.HasBuffer<ItemSlot>(receiver.BoundEntity))
                    {
                        receiver.UpdateFromBuffer(em.GetBuffer<ItemSlot>(receiver.BoundEntity));
                    }
                }
            }

            CenterWindow();
            OnWindowShown();
        }

        private void OnInternalWindowClosed()
        {
            // 🎯 Сохраняем текущую позицию ПЕРЕД скрытием окна
            SaveCurrentPosition();

            foreach (var receiver in _cachedReceivers)
            {
                if (receiver.BoundEntity != Entity.Null) UIRegistry.Unregister(receiver.BoundEntity, receiver);
            }
            OnWindowClosed();
        }

        // ============================================================
        // 🎯 ЦЕНТРИРОВАНИЕ С ВОССТАНОВЛЕНИЕМ ПОЗИЦИИ
        // ============================================================
        public void CenterWindow(int attempts = 0)
        {
            if (_root == null || _window == null || attempts > 20) return;

            // 🎯 Если для этого типа окна уже сохранена позиция — восстанавливаем её
            if (attempts == 0 && _savedPositions.TryGetValue(Type, out var saved))
            {
                _window.style.left = saved.x;
                _window.style.top = saved.y;
                _window.style.visibility = Visibility.Visible;
                return;
            }

            // Иначе — центрируем как раньше
            if (attempts == 0)
                _window.style.visibility = Visibility.Hidden;

            _root.schedule.Execute(() =>
            {
                if (_root == null || _window == null) return;
                if (!IsOpen) return;

                var w = _window.resolvedStyle.width;
                var h = _window.resolvedStyle.height;
                var pw = _root.resolvedStyle.width;
                var ph = _root.resolvedStyle.height;

                if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f || pw <= 0f || ph <= 0f)
                {
                    CenterWindow(attempts + 1);
                    return;
                }

                _window.style.left = (pw - w) / 2f;
                _window.style.top = (ph - h) / 2f;
                _window.style.visibility = Visibility.Visible;

            }).ExecuteLater(10);
        }

        // ============================================================
        // 🎯 СОХРАНЕНИЕ ПОЗИЦИИ
        // ============================================================
        private void SaveCurrentPosition()
        {
            if (_window == null) return;

            var leftLen = _window.style.left.value;
            var topLen = _window.style.top.value;

            // Если DragManipulator использовал пиксели — берём напрямую.
            // Иначе — фоллбэк на resolvedStyle.
            float left = leftLen.unit == LengthUnit.Pixel ? leftLen.value : _window.resolvedStyle.left;
            float top = topLen.unit == LengthUnit.Pixel ? topLen.value : _window.resolvedStyle.top;

            if (float.IsNaN(left) || float.IsNaN(top)) return;

            _savedPositions[Type] = new Vector2(left, top);
        }

        // 🎯 Чистый сброс кэша. Перецентрированием занимается WindowManager,
        // потому что именно он знает про открытые окна.
        public static void ClearSavedPositions() => _savedPositions.Clear();
        public static void ClearSavedPosition(WindowType type) => _savedPositions.Remove(type);      

        public virtual void Open() { if (_context != null) _context.Show(); }
        public virtual void Close()
        {
            if (DragManager.Instance != null && DragManager.Instance.IsDragging) DragManager.Instance.CancelDrag();
            if (_context != null) _context.Close();
        }
        public virtual void Toggle() { if (_context != null) _context.Toggle(); }

        protected virtual void OnWindowBuilt(VisualElement root) { }
        protected virtual void OnWindowShown() { }
        protected virtual void OnWindowClosed() { }
        public virtual void Setup(Entity entity) { }
        public virtual void Unbind() { }
    }
}

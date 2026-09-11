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
        protected WindowContext _context;

        private readonly List<IEcsUiBufferReceiver<ItemSlot>> _cachedReceivers = new();

        public VisualElement Root => _root;
        public bool IsOpen => _context != null && _context.IsVisible;

        public abstract WindowType Type { get; }
        public virtual Entity BoundEntity => Entity.Null;

        // 🦾 НАВЕДЕНА СТЕРИЛЬНОСТЬ: Окно больше ничего не ищет само!
        // Его инициализирует фабрика, передавая живой корень интерфейса сцены.
        public void InitializeWindow(VisualElement globalUiRoot)
        {
            if (globalUiRoot == null || _root != null) return;

            Debug.Log($"🏗️ [{GetType().Name}.InitializeWindow]: Получен живой root от фабрики. Собираем окно...");

            _root = _windowUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.pickingMode = PickingMode.Position;
            _root.style.display = DisplayStyle.None;
            
            // Врезаем в правильный, изолированный слой панели
            globalUiRoot.Add(_root);

            _context = new WindowContext(
                _root,
                onClose: OnInternalWindowClosed,
                onShow: OnInternalWindowShown
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
            OnWindowShown();
        }

        private void OnInternalWindowClosed()
        {
            foreach (var receiver in _cachedReceivers)
            {
                if (receiver.BoundEntity != Entity.Null) UIRegistry.Unregister(receiver.BoundEntity, receiver);
            }
            OnWindowClosed();
        }

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


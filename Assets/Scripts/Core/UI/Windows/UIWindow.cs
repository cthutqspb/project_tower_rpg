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
        protected PanelRenderer _panelRenderer;

        // Кэш для ECS-компонентов внутри этого окна, чтобы не сканировать дерево каждый кадр
        private List<IEcsUiBufferReceiver<SlotData>> _cachedReceivers = new();

        public VisualElement Root => _root;
        public bool IsOpen => _context != null && _context.IsVisible;

        protected virtual void Start()
        {
            _panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError($"[{GetType().Name}] PanelRenderer не найден!");
                return;
            }

            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        protected virtual void OnDestroy()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            if (_context != null) WindowManager.Pop(_context);
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _root != null) return;

            _root = _windowUxml.CloneTree();
            _root.pickingMode = PickingMode.Position;
            globalUiRoot.Add(_root);

            _context = new WindowContext(
                _root,
                onClose: OnInternalWindowClosed, // Подменяем на внутренний безопасный метод
                onShow: OnInternalWindowShown    // Подменяем на внутренний безопасный метод
            );

            _root.style.display = DisplayStyle.None;

            OnWindowBuilt(_root);

            // Сразу после сборки окна один раз сканируем его и находим все сетки/куклы на базе SlotData
            _cachedReceivers.Clear();
            _root.Query<VisualElement>().ForEach(element =>
            {
                if (element is IEcsUiBufferReceiver<SlotData> receiver)
                {
                    _cachedReceivers.Add(receiver);
                }
            });

            Debug.Log($"[{GetType().Name}] Окно собрано. Авто-найдено ECS-приемников: {_cachedReceivers.Count}");
        }

        // ================================================================
        // 🛡️ АВТОМАТИЧЕСКИЙ СИСТЕМНЫЙ СТЕК (Аналог твоего Lua-модуля M.init)
        // ================================================================

        private void OnInternalWindowShown()
        {
            // Автоматически регистрируем в UIRegistry ВСЕ сетки, куклы и панели, которые есть в этом окне
            foreach (var receiver in _cachedReceivers)
            {
                if (receiver.BoundEntity != Entity.Null)
                {
                    UIRegistry.Register(receiver.BoundEntity, receiver);
                    
                    // Сразу форсируем чтение свежих данных из ECS при открытии
                    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                    if (em.HasBuffer<SlotData>(receiver.BoundEntity))
                    {
                        receiver.UpdateFromBuffer(em.GetBuffer<SlotData>(receiver.BoundEntity));
                    }
                }
            }

            // Вызываем кастомный коллбэк дочернего класса, если он ему нужен
            OnWindowShown();
        }

        private void OnInternalWindowClosed()
        {
            // Автоматически ВЫПИСЫВАЕМ из UIRegistry абсолютно все ECS-компоненты окна
            foreach (var receiver in _cachedReceivers)
            {
                if (receiver.BoundEntity != Entity.Null)
                {
                    UIRegistry.Unregister(receiver.BoundEntity, receiver);
                }
            }

            // Вызываем кастомный коллбэк дочернего класса
            OnWindowClosed();
        }

        // ================================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ И ВИРТУАЛЬНЫЕ КОЛЛБЭКИ
        // ================================================================

        public virtual void Open() { if (_context != null) _context.Show(); }
        public virtual void Close()
        {
            // ✅ ОТМЕНЯЕМ ДРАГ ПРИ ЗАКРЫТИИ ЛЮБОГО ОКНА!
            if (DragManager.Instance != null && DragManager.Instance.IsDragging)
            {
                DragManager.Instance.CancelDrag();
                Debug.Log($"[{GetType().Name}] Драг отменён при закрытии окна.");
            }

            if (_context != null) _context.Close();
        }
        public virtual void Toggle() { if (_context != null) _context.Toggle(); }

        protected virtual void OnWindowBuilt(VisualElement root) { }
        protected virtual void OnWindowShown() { }
        protected virtual void OnWindowClosed() { }
    }
}


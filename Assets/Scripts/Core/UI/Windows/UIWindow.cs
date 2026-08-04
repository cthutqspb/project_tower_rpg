using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    /// <summary>
    /// Максимально абстрактный базовый класс для всех окон.
    /// Никаких хедеров, боди, футеров — только регистрация в WindowManager.
    /// </summary>
    public abstract class UIWindow : MonoBehaviour
    {
        [SerializeField] protected VisualTreeAsset _windowUxml;
        
        protected VisualElement _root;
        protected WindowContext _context;
        protected PanelRenderer _panelRenderer;

        // Свойства для доступа извне
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
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }

            if (_context != null)
            {
                WindowManager.Pop(_context);
            }
        }

        // ================================================================
        // UI СБОРКА
        // ================================================================

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _root != null) return;

            _root = _windowUxml.CloneTree();
            _root.pickingMode = PickingMode.Position;
            globalUiRoot.Add(_root);

            // Создаём контекст для регистрации в WindowManager
            _context = new WindowContext(
                _root,
                onClose: OnWindowClosed,
                onShow: OnWindowShown
            );

            // По умолчанию окно скрыто
            _root.style.display = DisplayStyle.None;

            OnWindowBuilt(_root);

            Debug.Log($"[{GetType().Name}] Окно собрано");
        }

        // ================================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ
        // ================================================================

        public virtual void Open()
        {
            if (_context == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Окно ещё не инициализировано");
                return;
            }

            _context.Show();
        }

        public virtual void Close()
        {
            if (_context == null) return;
            _context.Close();
        }

        public virtual void Toggle()
        {
            if (_context == null) return;
            _context.Toggle();
        }

        // ================================================================
        // ВИРТУАЛЬНЫЕ МЕТОДЫ ДЛЯ ДОЧЕРНИХ КЛАССОВ
        // ================================================================

        /// <summary>
        /// Вызывается после сборки UI. Здесь дочерние классы добавляют свои компоненты.
        /// </summary>
        protected virtual void OnWindowBuilt(VisualElement root) { }

        /// <summary>
        /// Вызывается при показе окна.
        /// </summary>
        protected virtual void OnWindowShown() { }

        /// <summary>
        /// Вызывается при закрытии окна.
        /// </summary>
        protected virtual void OnWindowClosed() { }

        // ================================================================
        // ОБНОВЛЕНИЕ ДАННЫХ
        // ================================================================

        /// <summary>
        /// Обновить содержимое окна. Дочерние классы переопределяют.
        /// </summary>
        public virtual void Refresh() { }
    }
}

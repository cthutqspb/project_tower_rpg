using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    [Serializable]
    public struct WindowPrefabEntry
    {
        [SerializeField] private WindowType _type;
        [SerializeField] private UIWindow _prefab;

        public WindowType Type => _type;
        public UIWindow Prefab => _prefab;
    }

    public class WindowFactory : MonoBehaviour
    {
        public static WindowFactory Instance { get; private set; }

        [SerializeField] private List<WindowPrefabEntry> _prefabs = new();

        private Dictionary<WindowType, Queue<UIWindow>> _pool = new();
        private PanelRenderer _panelRenderer;
        
        // 🦾 ХРАНИМ ЖИВОЙ КОРЕНЬ: Фабрика кэширует нативный слой рендера при старте сцены
        private VisualElement _cachedGlobalUiRoot;
        private bool _isUiReady;

        private void Awake()
        {
            Instance = this;
            _panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);

                // =========================================================================
                // 🦾 TODO: АППАРАТНЫЙ ПРОГРЕВ ПУЛА ОКON (Warmup)
                // =========================================================================
                // Написать корутину или плоский Си-метод прогрева пула окон, который
                // вхолостую сделает по одному вызову GetWindow() для каждого типа из WindowType
                // и сразу вернет их в ReturnToPool(). 
                // Это выполнит тяжелые Instantiate и CloneTree ДО начала игры, 
                // полностью стерев микрофриз при первом открытии окон инвентаря и добычи!
                // =========================================================================
            }
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement root, int version)
        {
            // Нативный PanelRenderer сам прислал нам легальный рабочий корень экрана!
            _cachedGlobalUiRoot = root;
            _isUiReady = true;
            Debug.Log("🌐 [WindowFactory]: Успешно перехвачен и закэширован живой root от PanelRenderer!");
        }

        private void OnEnable()
        {
            UIEvents.OnOpenWindow += OnOpenWindowRequested;
            UIEvents.OnCloseWindow += OnCloseWindowRequested;
            UIEvents.CloseAllWindows += CloseAll;
        }

        private void OnDisable()
        {
            UIEvents.OnOpenWindow -= OnOpenWindowRequested;
            UIEvents.OnCloseWindow -= OnCloseWindowRequested;
            UIEvents.CloseAllWindows -= CloseAll;
        }

        private void OnOpenWindowRequested(WindowType type, Entity entity, string tabId)
        {
            Debug.Log($"🔔 [WindowFactory]: Поймали запрос на {type} для сущности {entity.Index}.");

            // 🦾 ЮНИТИ 6 КАНОН: Ищем только среди АКТИВНЫХ окон на сцене, без устаревшей сортировки
            var activeWindows = FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude);
            
            foreach (var activeWindow in activeWindows)
            {
                // 🦾 ТОТАЛЬНЫЙ ОЧИЩЕННЫЙ TOGGLE:
                // Если окно этого типа горит на экране, и либо сущности равны,
                // либо пришел дефолтный запрос (Entity.Null), а открытое окно тоже дефолтное — ЗАКРЫВАЕМ ЕГО!
                if (activeWindow.IsOpen && activeWindow.Type == type)
                {
                    if (activeWindow.BoundEntity == entity || entity == Entity.Null)
                    {
                        if (TryOpenTabOnActive(activeWindow, tabId, type, entity)) return;
                        return;
                    }
                }
            }

            Debug.Log($"⚙️ [WindowFactory]: Дубликатов на экране нет. Достаем окно из пула для типа {type}...");
            UIWindow window = GetWindow(type);
            if (window == null) return;

            // Слепо скармливаем сущность. Окно внутри себя вызовет Open()
            window.Setup(entity);

            // 🆕 После Setup — переключаем вкладку, если окно поддерживает
            if (!string.IsNullOrEmpty(tabId) && window is ITabbedWindow tabbed)
            {
                tabbed.OpenOnTabId(tabId);
            }
        }

        private bool TryOpenTabOnActive(UIWindow activeWindow, string tabId, WindowType type, Entity entity)
        {
            // tabId задан (J) — просим окно переключиться. Окно само скажет, обработало ли.
            if (!string.IsNullOrEmpty(tabId) && activeWindow is ITabbedWindow tabbed)
            {
                bool handled = tabbed.OpenOnTabId(tabId);
                if (!handled)
                {
                    // Окно уже на этой вкладке → просит закрыть
                    UIEvents.TriggerCloseWindow(type, activeWindow.BoundEntity);
                }
                return true;
            }

            // tabId не задан (I) — обычный toggle-close
            UIEvents.TriggerCloseWindow(type, activeWindow.BoundEntity);
            return true;
        }

        private void OnCloseWindowRequested(WindowType type, Entity entity)
        {
            // 🦾 ЮНИТИ 6 КАНОН: Вырезали FindObjectsSortMode
            var activeWindows = FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude);
            foreach (var activeWindow in activeWindows)
            {
                if (activeWindow.Type == type && (activeWindow.BoundEntity == entity || entity == Entity.Null))
                {
                    ReturnToPool(type, activeWindow);
                    return;
                }
            }
        }
        
        private void CloseAll()
        {
            Debug.Log("💤 [WindowFactory]: Поймали глобальный сигнал CloseAllWindows. Синхронизируем пул...");

            // 🦾 ЮНИТИ 6 КАНОН: Стерильный поиск без устаревших параметров
            var allWindows = FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude);
            
            foreach (var window in allWindows)
            {
                if (window.IsOpen)
                {
                    ReturnToPool(window.Type, window);
                }
            }
        }
        
        public UIWindow GetWindow(WindowType type)
        {
            if (!_isUiReady || _cachedGlobalUiRoot == null) return null;

            if (_pool.TryGetValue(type, out var queue) && queue.Count > 0)
            {
                var window = queue.Dequeue();
                window.IsInPool = false;
                return window;
            }

            UIWindow prefab = null;
            foreach (var entry in _prefabs)
            {
                if (entry.Type == type) { prefab = entry.Prefab; break; }
            }

            if (prefab == null) return null;

            // Спавним префаб
            UIWindow windowInstance = Instantiate(prefab);
                        
            // 🦾 ШЛЮЗ БЕЗОПАСНОСТИ: Фабрика передает окну легальный корень рендера прямо в руки!
            windowInstance.InitializeWindow(_cachedGlobalUiRoot);

            return windowInstance;
        }

        public void ReturnToPool(WindowType type, UIWindow window)
        {
            if (window == null) return;
            if (window.IsInPool) return;   // ← защита

            window.Close();
            window.IsInPool = true;

            if (!_pool.TryGetValue(type, out var queue))
            {
                queue = new Queue<UIWindow>();
                _pool[type] = queue;
            }
            queue.Enqueue(window);
        }
    }
}


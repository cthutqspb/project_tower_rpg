using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class ChatLog
    {
        private sealed class TabDef
        {
            public LogChannel Channel;
            public Button Button;
            public string ActiveClass = "chat-tab-active";
            public System.Func<LogChannel, bool> Filter;
        }

        private readonly List<TabDef> _tabs = new();
        private TabDef _currentTab;

        private const int MaxLogLines = 100;
        
        private readonly VisualElement _root;
        private readonly VisualElement _windowBody;
        private ScrollView _messagesContainer;
        
        private Button _lockBtn;

        // Архитектурное ОЗУ-хранилище всех логов для горячего переключения вкладок
        private readonly List<LogLine> _allLinesCache = new();
        
        private bool _isLocked = false;
        
        private DragManipulator _dragManipulator;

        public ChatLog(VisualElement rootVisual)
        {
            _root = rootVisual;
            
            // Выуживаем тело окна из TemplateContainer для навешивания модификатора .chat-locked
            _windowBody = _root.Q<VisualElement>("chat-log-root");
            _messagesContainer = _root.Q<ScrollView>("chat-log-messages-container");
            
            _lockBtn = _root.Q<Button>("chat-lock-btn");
            _lockBtn.text = "";

            if (_messagesContainer == null || _windowBody == null)
            {
                Debug.LogError("[ChatLog]: Критическая ошибка сборки UXML дерева лога!");
                return;
            }

            // Инициализируем табы
            InitTabs();

            if (_lockBtn != null) _lockBtn.clicked += ToggleLockState;

            // 🦾 ВНEДРЕНИЕ ДРAГ-МАНИПУЛЯТОРА ПО КАНОНУ ПРОЕКТА:
            // Хедером/ручкой для перетаскивания выступает вся верхняя панель chat-toolbar.
            // Перетаскиваем мы ВНЕШНЮЮ обертку _root (TemplateContainer с классом .chat-log-wrapper)!
            var toolbar = _root.Q<VisualElement>("chat-log-toolbar");
            if (toolbar != null)
            {
                _root.RegisterCallback<PointerDownEvent>(evt => _root.BringToFront());
                _dragManipulator = new DragManipulator(toolbar, _root, DragMode.UIElement);
                toolbar.AddManipulator(_dragManipulator);
            }
            
            _messagesContainer.RegisterCallback<GeometryChangedEvent>(OnMessagesGeometryChanged);

            // Старт подписок на unmanaged шину бродкаста
            LogBroadcast.OnMessageReceived += HandleIncomingMessage;
            LogBroadcast.OnClearRequested += HandleClearChat;

            LogBroadcast.Send(LogChannel.System, "⚔️ [Система]: Чат-лог переведен в UDF-режим фильтрации вкладок.");
        }

        private void InitTabs()
        {
            _tabs.Add(new TabDef
            {
                Channel = LogChannel.System,
                Button = _root.Q<Button>("tab-system-btn"),
                ActiveClass = "chat-tab-active",
                Filter = ch => ch != LogChannel.Combat && ch != LogChannel.World // всё, кроме боя и мира
            });
            _tabs.Add(new TabDef
            {
                Channel = LogChannel.Combat,
                Button = _root.Q<Button>("tab-combat-btn"),
                ActiveClass = "chat-tab-active",
                Filter = ch => ch == LogChannel.Combat
            });
            _tabs.Add(new TabDef
            {
                Channel = LogChannel.World,
                Button = _root.Q<Button>("tab-world-btn"),
                ActiveClass = "chat-tab-active",
                Filter = ch => ch == LogChannel.World
            });

            foreach (var tab in _tabs)
            {
                if (tab.Button == null) continue;
                var captured = tab; // замыкание по ссылке, а не по переменной цикла
                tab.Button.clicked += () => SwitchTab(captured);
            }

            _currentTab = _tabs[0];
            if (_currentTab.Button != null)
                _currentTab.Button.AddToClassList(_currentTab.ActiveClass);
        }

        public void Dispose()
        {
            LogBroadcast.OnMessageReceived -= HandleIncomingMessage;
            LogBroadcast.OnClearRequested -= HandleClearChat;
            _messagesContainer?.UnregisterCallback<GeometryChangedEvent>(OnMessagesGeometryChanged);
        }

        private void OnMessagesGeometryChanged(GeometryChangedEvent evt)
        {
            ScrollToBottom();
        }

        private void HandleIncomingMessage(LogLine line)
        {
            // Кэшируем абсолютно все прилетающие пакеты логов в ОЗУ
            _allLinesCache.Add(line);
            if (_allLinesCache.Count > MaxLogLines * 2)
            {
                _allLinesCache.RemoveAt(0);
            }

            if (_currentTab.Filter(line.Channel))
                RenderSingleLine(line.FormattedText);
        }


        private void RenderSingleLine(string formattedText)
        {
            var label = new Label
            {
                text = formattedText,
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList("chat-line-item");
            
            _messagesContainer.Add(label);

            if (_messagesContainer.childCount > MaxLogLines)
            {
                _messagesContainer.RemoveAt(0);
            }
        }

        private void SwitchTab(TabDef newTab)
        {
            if (_currentTab == newTab) return;
            _currentTab = newTab;

            foreach (var tab in _tabs)
            {
                if (tab.Button == null) continue;
                if (tab == newTab) tab.Button.AddToClassList(tab.ActiveClass);
                else                tab.Button.RemoveFromClassList(tab.ActiveClass);
            }

            RebuildFeed();
        }

        private void RebuildFeed()
        {
            _messagesContainer.Clear();
            foreach (var line in _allLinesCache)
            {
                if (_currentTab.Filter(line.Channel))
                    RenderSingleLine(line.FormattedText);
            }
        }

        private void ToggleLockState()
        {
            _isLocked = !_isLocked;

            if (_isLocked)
            {
                // Замок закрыт: вешаем USS-модификатор скрытия рамок, меняем иконку на Закрыто
                _windowBody.AddToClassList("chat-locked");
                if (_lockBtn != null) _lockBtn.text = ""; // Иконка закрытого замка Nerd Font
                
                // На Силе отключаем DragManipulator, убирая его из тулбара!
                var toolbar = _root.Q<VisualElement>("chat-log-toolbar");
                if (toolbar != null && _dragManipulator != null)
                {
                    toolbar.RemoveManipulator(_dragManipulator);
                }
            }
            else
            {
                // Замок открыт: возвращаем рамку интерфейса, иконку Открыто и Драг обратно
                _windowBody.RemoveFromClassList("chat-locked");
                if (_lockBtn != null) _lockBtn.text = "󰿇"; // Иконка открытого замка Nerd Font
                
                var toolbar = _root.Q<VisualElement>("chat-log-toolbar");
                if (toolbar != null && _dragManipulator != null)
                {
                    toolbar.AddManipulator(_dragManipulator);
                }
            }
        }

        private void ScrollToBottom()
        {
            if (_messagesContainer == null) return;
            _messagesContainer.scrollOffset = new Vector2(0f, _messagesContainer.layout.height);
        }

        private void HandleClearChat()
        {
            _allLinesCache.Clear();
            _messagesContainer?.Clear();
        }
    }
}


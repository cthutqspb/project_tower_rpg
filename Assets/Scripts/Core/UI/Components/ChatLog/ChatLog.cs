using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class ChatLog
    {
        private const int MaxLogLines = 100;

        private readonly VisualElement _root;
        private readonly VisualElement _windowBody;
        private ScrollView _messagesContainer;

        private Button _lockBtn;
        private TabsComponent _tabsComponent;

        // Архитектурное ОЗУ-хранилище всех логов для горячего переключения вкладок
        private readonly List<LogLine> _allLinesCache = new();

        private bool _isLocked = false;
        private ChatLogTabs _activeTab = ChatLogTabs.System;

        private DragManipulator _dragManipulator;

        public ChatLog(VisualElement rootVisual)
        {
            _root = rootVisual;

            _windowBody = _root.Q<VisualElement>("root");
            _messagesContainer = _root.Q<ScrollView>("messages-container");

            _lockBtn = _root.Q<Button>("lock-btn");
            if (_lockBtn != null) _lockBtn.text = "";

            if (_messagesContainer == null || _windowBody == null)
            {
                Debug.LogError("[ChatLog]: Критическая ошибка сборки UXML дерева лога!");
                return;
            }

            InitTabs();

            if (_lockBtn != null) _lockBtn.clicked += ToggleLockState;

            var toolbar = _root.Q<VisualElement>("toolbar");
            if (toolbar != null)
            {
                _root.RegisterCallback<PointerDownEvent>(evt => _root.BringToFront());
                _dragManipulator = new DragManipulator(toolbar, _root, DragMode.UIElement);
                toolbar.AddManipulator(_dragManipulator);
            }

            _messagesContainer.RegisterCallback<GeometryChangedEvent>(OnMessagesGeometryChanged);

            LogBroadcast.OnMessageReceived += HandleIncomingMessage;
            LogBroadcast.OnClearRequested += HandleClearChat;

            LogBroadcast.Send(LogChannel.System, "⚔️ [Система]: Чат-лог переведен в UDF-режим фильтрации вкладок.");
        }

        private void InitTabs()
        {
            var defs = new List<TabDefinition>
            {
                new TabDefinition { Name = "tab-system-btn", Label = "Система" },
                new TabDefinition { Name = "tab-combat-btn", Label = "Бой" },
                new TabDefinition { Name = "tab-world-btn",  Label = "Мир" },
            };

            _tabsComponent = new TabsComponent(
                _root.Q<VisualElement>("tabs"),
                defs,
                onChanged: idx =>
                {
                    _activeTab = (ChatLogTabs)idx;
                    RebuildFeed();
                },
                activeClassName: "chat-log__tab-active",
                defaultIndex: (int)ChatLogTabs.System
            );
        }

        public void Dispose()
        {
            LogBroadcast.OnMessageReceived -= HandleIncomingMessage;
            LogBroadcast.OnClearRequested -= HandleClearChat;
            _messagesContainer?.UnregisterCallback<GeometryChangedEvent>(OnMessagesGeometryChanged);
            _tabsComponent?.Dispose();
        }

        private void OnMessagesGeometryChanged(GeometryChangedEvent evt)
        {
            ScrollToBottom();
        }

        // ============================================================
        // ФИЛЬТР ПО АКТИВНОМУ ТАБУ
        // ============================================================
        private bool PassesFilter(LogChannel channel)
        {
            switch (_activeTab)
            {
                case ChatLogTabs.System:
                    return channel != LogChannel.Combat && channel != LogChannel.World;
                case ChatLogTabs.Combat:
                    return channel == LogChannel.Combat;
                case ChatLogTabs.World:
                    return channel == LogChannel.World;
                default:
                    return true;
            }
        }

        private void HandleIncomingMessage(LogLine line)
        {
            _allLinesCache.Add(line);
            if (_allLinesCache.Count > MaxLogLines * 2)
                _allLinesCache.RemoveAt(0);

            if (PassesFilter(line.Channel))
                RenderSingleLine(line.FormattedText);
        }

        private void RenderSingleLine(string formattedText)
        {
            var label = new Label
            {
                text = formattedText,
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList("chat-log__line-item");

            _messagesContainer.Add(label);

            if (_messagesContainer.childCount > MaxLogLines)
                _messagesContainer.RemoveAt(0);
        }

        private void RebuildFeed()
        {
            _messagesContainer.Clear();
            foreach (var line in _allLinesCache)
            {
                if (PassesFilter(line.Channel))
                    RenderSingleLine(line.FormattedText);
            }
        }

        private void ToggleLockState()
        {
            _isLocked = !_isLocked;

            if (_isLocked)
            {
                _windowBody.AddToClassList("chat-log__locked");
                if (_lockBtn != null) _lockBtn.text = "";

                var toolbar = _root.Q<VisualElement>("toolbar");
                if (toolbar != null && _dragManipulator != null)
                    toolbar.RemoveManipulator(_dragManipulator);
            }
            else
            {
                _windowBody.RemoveFromClassList("chat-log__locked");
                if (_lockBtn != null) _lockBtn.text = "󰿇";

                var toolbar = _root.Q<VisualElement>("toolbar");
                if (toolbar != null && _dragManipulator != null)
                    toolbar.AddManipulator(_dragManipulator);
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

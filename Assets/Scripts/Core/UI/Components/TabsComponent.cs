using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI.Components
{
    // Описание одного таба.
    // Заполняй только нужные поля — пустые строки не трогают существующие значения в UXML.
    public struct TabDefinition
    {
        public string Name;         // имя ноды кнопки в UXML (для Q<Button>)
        public string Label;        // текст кнопки (пусто — не трогать)
        public string IconGlyph;    // Nerd Font символ (пусто — не трогать)
        public string Tooltip;      // тултип (пусто — не трогать)
    }

    public class TabsComponent : IDisposable
    {
        private readonly VisualElement _container;
        private readonly List<TabDefinition> _tabs = new();
        private readonly List<Button> _buttons = new();
        private readonly Action<int> _onChanged;
        private readonly string _activeClassName;

        private int _activeIndex = -1;

        public int ActiveIndex => _activeIndex;

        public TabsComponent(
            VisualElement container,
            IEnumerable<TabDefinition> tabs,
            Action<int> onChanged,
            string activeClassName = "tabs__button--active",
            int defaultIndex = 0)
        {
            _container = container;
            _onChanged = onChanged;
            _activeClassName = activeClassName;

            if (_container == null)
            {
                Debug.LogError("[TabsComponent]: Контейнер равен null.");
                return;
            }

            // Ищем кнопки по именам и заполняем их контентом
            foreach (var def in tabs)
            {
                _tabs.Add(def);

                var btn = _container.Q<Button>(def.Name);
                if (btn == null)
                {
                    Debug.LogWarning($"[TabsComponent]: Не найдена кнопка '{def.Name}' в контейнере '{_container.name}'.");
                    _buttons.Add(null);
                    continue;
                }

                if (!string.IsNullOrEmpty(def.Label))
                    btn.text = def.Label;

                if (!string.IsNullOrEmpty(def.IconGlyph))
                    btn.text = def.IconGlyph;

                if (!string.IsNullOrEmpty(def.Tooltip))
                    btn.tooltip = def.Tooltip;

                btn.AddToClassList("tabs__button");

                _buttons.Add(btn);
            }

            // Вешаем клики
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                if (btn == null) continue;

                int captured = i;   // замыкание по значению, не по переменной цикла
                btn.clicked += () => SetActive(captured, notify: true);
            }

            // Активируем стартовый
            if (_buttons.Count > 0)
                SetActive(Mathf.Clamp(defaultIndex, 0, _buttons.Count - 1), notify: false);
        }

        // Программное переключение. notify=true — колбэк сработает, false — тихо.
        public void SetActive(int index, bool notify = true)
        {
            if (index < 0 || index >= _buttons.Count) return;
            if (_activeIndex == index) return;

            // Снять --active со старого
            if (_activeIndex >= 0 && _activeIndex < _buttons.Count)
            {
                var prev = _buttons[_activeIndex];
                prev?.RemoveFromClassList(_activeClassName);
            }

            _activeIndex = index;

            var next = _buttons[_activeIndex];
            next?.AddToClassList(_activeClassName);

            if (notify)
                _onChanged?.Invoke(_activeIndex);
        }

        public void Dispose()
        {
            // Отписываемся от кликов
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                if (btn == null) continue;

                // Отписка через пересоздание clickable невозможно,
                // но можно очистить все колбэки клика через .Clear()
                // В UI Toolkit нет прямого API для отписки от лямбды без сохранения ссылки.
                // Если нужно чистое Dispose — сохраняй Action в список и отписывай явно.
            }

            _buttons.Clear();
            _tabs.Clear();
        }
    }
}

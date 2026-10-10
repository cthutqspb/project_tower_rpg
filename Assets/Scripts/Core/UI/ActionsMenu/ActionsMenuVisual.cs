using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI
{
    public class ActionsMenuVisual : VisualElement
    {
        private VisualElement _list;
        private readonly List<Button> _buttons = new();
        private bool _isVisible = false;

        public ActionsMenuVisual()
        {
            this.style.position = Position.Absolute;
            this.style.display = DisplayStyle.None;
            this.pickingMode = PickingMode.Ignore;
        }

        public void Show(Vector2 screenPosition, List<MenuAction> actions, object menuActionData = null)
        {
            if (actions == null || actions.Count == 0)
            {
                Hide();
                return;
            }

            // Очищаем прошлое
            this.Clear();
            _buttons.Clear();

            // this — корень меню
            this.AddToClassList("context-menu");
            this.style.left = screenPosition.x;
            this.style.top = screenPosition.y;
            this.pickingMode = PickingMode.Position;

            // список кнопок
            _list = new VisualElement();
            _list.AddToClassList("context-menu__list");
            _list.pickingMode = PickingMode.Position;
            this.Add(_list);

            _list.RegisterCallback<PointerDownEvent>(OnBackgroundClick);

            foreach (var action in actions)
            {
                var button = CreateButton(action, menuActionData);
                _list.Add(button);
                _buttons.Add(button);
            }

            this.style.display = DisplayStyle.Flex;
            _isVisible = true;
            this.BringToFront();
        }

        public void Hide()
        {
            this.Clear();
            this.RemoveFromClassList("context-menu");
            this.style.display = DisplayStyle.None;
            this.pickingMode = PickingMode.Ignore;
            _list = null;
            _buttons.Clear();
            _isVisible = false;
        }

        public bool IsVisible() => _isVisible;

        private Button CreateButton(MenuAction action, object contextData)
        {
            var button = new Button();
            button.text = LocalizationManager.Get(action.NameKey);
            button.AddToClassList("context-menu__button");

            button.clicked += () =>
            {
                CreateActionCommand(action, contextData);
                Hide();
            };

            return button;
        }

        private void OnBackgroundClick(PointerDownEvent evt)
        {
            Hide();
        }

        private void CreateActionCommand(MenuAction action, object menuActionData)
        {
            var data = menuActionData as ActionsMenu.MenuActionData;
            if (data == null) return;

            UIEvents.TriggerMenuActionSelected(action, data);
        }
    }
}

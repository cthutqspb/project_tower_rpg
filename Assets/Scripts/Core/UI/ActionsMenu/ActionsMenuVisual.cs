using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.Core.UI
{
    public class ActionsMenuVisual : VisualElement
    {
        private VisualElement _root;
        private VisualElement _listContainer;
        private List<Button> _buttons = new List<Button>();
        private EntityManager _entityManager;
        private bool _isVisible = false;

        public ActionsMenuVisual(EntityManager entityManager)
        {
            _entityManager = entityManager;
            this.style.position = Position.Absolute;
            this.style.display = DisplayStyle.None;
            this.pickingMode = PickingMode.Ignore;
            Debug.Log("[ActionsMenuVisual] Конструктор");
        }

        public void Show(Vector2 screenPosition, List<MenuAction> actions, object menuActionData = null)
        {
            Debug.Log($"[ActionsMenuVisual] Show() actions={actions?.Count ?? 0}, pos={screenPosition}");

            if (actions == null || actions.Count == 0)
            {
                Hide();
                return;
            }

            // ✅ ДИНАМИЧЕСКИ СОЗДАЁМ МЕНЮ ПРЯМО СЕЙЧАС
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            _root.style.borderTopLeftRadius = 6;
            _root.style.borderTopRightRadius = 6;
            _root.style.borderBottomLeftRadius = 6;
            _root.style.borderBottomRightRadius = 6;
            _root.style.paddingTop = 4;
            _root.style.paddingBottom = 4;
            _root.style.paddingLeft = 4;
            _root.style.paddingRight = 4;
            _root.style.minWidth = 180;
            _root.style.left = screenPosition.x;
            _root.style.top = screenPosition.y;
            _root.pickingMode = PickingMode.Position;
            Add(_root);

            _listContainer = new VisualElement();
            _listContainer.style.flexDirection = FlexDirection.Column;
            _listContainer.pickingMode = PickingMode.Position;
            _root.Add(_listContainer);

            // Закрытие по клику мимо
            _listContainer.RegisterCallback<PointerDownEvent>(OnBackgroundClick);

            // Создаём кнопки
            foreach (var action in actions)
            {
                var button = CreateButton(action, menuActionData);
                _listContainer.Add(button);
                _buttons.Add(button);
            }

            this.style.display = DisplayStyle.Flex;
            this.pickingMode = PickingMode.Position;
            _isVisible = true;
            this.BringToFront();
            Debug.Log("[ActionMenuVisual] Меню показано");
        }

        public void Hide()
        {
            Debug.Log("[ActionMenuVisual] Hide()");
            
            // ✅ УДАЛЯЕМ ВСЁ, ЧТО СОЗДАЛИ
            if (_root != null)
            {
                Remove(_root);
                _root = null;
                _listContainer = null;
            }
            
            ClearButtons();
            this.style.display = DisplayStyle.None;
            this.pickingMode = PickingMode.Ignore;
            _isVisible = false;
        }

        public bool IsVisible() => _isVisible;

        private Button CreateButton(MenuAction action, object contextData)
        {
            var button = new Button();
            button.text = action.NameKey;
            button.style.marginBottom = 2;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.backgroundColor = Color.clear;
            button.style.color = Color.white;
            button.style.fontSize = 13;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;

            button.RegisterCallback<PointerOverEvent>(_ => button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f));
            button.RegisterCallback<PointerOutEvent>(_ => button.style.backgroundColor = Color.clear);

            button.clicked += () =>
            {
                CreateActionCommand(action, contextData);
                Hide();
            };

            return button;
        }

        private void ClearButtons()
        {
            foreach (var button in _buttons) button.RemoveFromHierarchy();
            _buttons.Clear();
        }

        private void OnBackgroundClick(PointerDownEvent evt)
        {
            Debug.Log("[ActionMenuVisual] OnBackgroundClick");
            Hide();
        }

        private void CreateActionCommand(MenuAction action, object menuActionData)
        {
            if (_entityManager == null) return;

            var data = menuActionData as ActionsMenu.MenuActionData;
            if (data == null) return;

            var actionEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(actionEntity, new ActionCommand
            {
                ActionType = action.ActionType,
                SourceEntity = data.ContainerEntity,
                SourceSlot = data.SlotIndex,
                ItemId = data.ItemId,
                Amount = data.Amount,
            });

            Debug.Log($"[ActionsMenu] Выполнено действие: {action.ActionType} для {data.ItemId}");
        }
    }
}

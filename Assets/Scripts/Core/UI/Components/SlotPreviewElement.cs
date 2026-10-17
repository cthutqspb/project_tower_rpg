using System;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI.Components
{
    // Универсальный превью-элемент: иконка (с тултипом через SlotElement) + название.
    // Не перетаскивается, не лежит в инвентаре. Для показа наград.
    public class SlotPreviewElement : VisualElement
    {
        private SlotElement _slot;
        private Label _nameLabel;

        public string ItemId { get; private set; }
        public bool IsChoice { get; set; }

        public event Action<SlotPreviewElement> OnClicked;

        public SlotPreviewElement()
        {
            AddToClassList("slot-preview");

            _slot = new SlotElement();
            _slot.SetEnabled(false);   // отключает drag и клики слота
            Add(_slot);

            _nameLabel = new Label();
            _nameLabel.AddToClassList("slot-preview__name");
            Add(_nameLabel);

            RegisterCallback<ClickEvent>(OnClick);
        }

        public void SetItem(string itemId, int amount = 1)
        {
            ItemId = itemId;

            _slot.SetData(itemId, 0, amount);

            var config = ItemsDatabase.GetItem(itemId);
            _nameLabel.text = config != null
                ? LocalizationManager.Get(config.identity.name_key)
                : itemId;
        }

        public void Clear()
        {
            ItemId = null;
            _slot.SetData("", 0, 0);
            _nameLabel.text = "";
        }

        public void SetSelected(bool selected)
        {
            if (selected)
                AddToClassList("slot-preview--selected");
            else
                RemoveFromClassList("slot-preview--selected");
        }

        public void SetChoice(bool isChoice)
        {
            IsChoice = isChoice;
            if (isChoice)
                AddToClassList("slot-preview--choice");
            else
                RemoveFromClassList("slot-preview--choice");
        }

        private void OnClick(ClickEvent evt)
        {
            if (!IsChoice || string.IsNullOrEmpty(ItemId)) return;

            OnClicked?.Invoke(this);
            evt.StopPropagation();
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class SlotElement : VisualElement
    {
        private VisualElement _icon;
        private VisualElement _cooldownOverlay;
        private Label _bindLabel;
        private Label _amountLabel;
        private Label _durationLabel;
        private string _itemId;

        public int SlotIndex { get; set; }
        public Entity InventoryEntity { get; set; }
        public string DataSourceId { get; set; }
        public string GridType { get; set; }
        public object Source { get; set; }

        public SlotElement()
        {
            this.AddToClassList("slot");
            this.style.width = 40;
            this.style.height = 40;
            this.style.marginLeft = 2;
            this.style.marginRight = 2;
            this.style.marginTop = 2;
            this.style.marginBottom = 2;
            this.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            
            _icon = new VisualElement();
            _icon.AddToClassList("slot-icon");
            _icon.style.width = 40;
            _icon.style.height = 40;
            _icon.style.display = DisplayStyle.None;
            Add(_icon);
            
            _cooldownOverlay = new VisualElement();
            _cooldownOverlay.AddToClassList("slot-cooldown-overlay");
            _cooldownOverlay.style.width = 40;
            _cooldownOverlay.style.height = 40;
            _cooldownOverlay.style.backgroundColor = new Color(0, 0, 0, 0);
            Add(_cooldownOverlay);
            
            _bindLabel = new Label();
            _bindLabel.AddToClassList("slot-bind-label");
            _bindLabel.style.display = DisplayStyle.None;
            Add(_bindLabel);
            
            _amountLabel = new Label();
            _amountLabel.AddToClassList("slot-amount-label");
            _amountLabel.style.display = DisplayStyle.None;
            Add(_amountLabel);
            
            _durationLabel = new Label();
            _durationLabel.AddToClassList("slot-duration-label");
            _durationLabel.style.display = DisplayStyle.None;
            Add(_durationLabel);

            var dragManipulator = new DragManipulator(
                target: this,
                mode: DragMode.Slot
            );
            this.AddManipulator(dragManipulator);
        }

        public void SetData(string itemId, ItemConfig config, string gridType, int index, int amount = 1)
        {
            _itemId = itemId;
            GridType = gridType;
            SlotIndex = index;
            ClearVisual();

            if (config == null || string.IsNullOrEmpty(itemId))
            {
                return;
            }

            _icon.style.display = DisplayStyle.Flex;
            _icon.style.backgroundColor = GetQualityColor(config.identity.quality);

            if (gridType == "action_bar")
            {
                _bindLabel.style.display = DisplayStyle.Flex;
                _bindLabel.text = GetBindKey(index);
            }

            if (amount > 1)
            {
                _amountLabel.style.display = DisplayStyle.Flex;
                _amountLabel.text = amount.ToString();
            }
            else
            {
                _amountLabel.style.display = DisplayStyle.None;
                _amountLabel.text = "";
            }
        }

        public void Refresh()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || InventoryEntity == Entity.Null)
            {
                ClearVisual();
                return;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.HasBuffer<SlotData>(InventoryEntity))
            {
                ClearVisual();
                return;
            }

            var slots = entityManager.GetBuffer<SlotData>(InventoryEntity);
            if (SlotIndex < 0 || SlotIndex >= slots.Length)
            {
                ClearVisual();
                return;
            }

            var slot = slots[SlotIndex];
            
            if (string.IsNullOrEmpty(slot.DataId.ToString()) || slot.Amount <= 0)
            {
                ClearVisual();
                return;
            }

            var itemId = slot.DataId.ToString();
            var config = ItemsDatabase.GetItem(itemId);
            
            if (config == null)
            {
                _icon.style.display = DisplayStyle.Flex;
                _icon.style.backgroundColor = Color.gray;
                return;
            }

            _icon.style.display = DisplayStyle.Flex;
            _icon.style.backgroundColor = GetQualityColor(config.identity.quality);

            if (GridType == "action_bar")
            {
                _bindLabel.style.display = DisplayStyle.Flex;
                _bindLabel.text = GetBindKey(SlotIndex);
            }

            if (slot.Amount > 1)
            {
                _amountLabel.style.display = DisplayStyle.Flex;
                _amountLabel.text = slot.Amount.ToString();
            }
            else
            {
                _amountLabel.style.display = DisplayStyle.None;
                _amountLabel.text = "";
            }
        }

        public void ClearVisual()
        {
            _itemId = "";
            _icon.style.display = DisplayStyle.None;
            _icon.style.backgroundColor = Color.clear;
            _bindLabel.text = "";
            _bindLabel.style.display = DisplayStyle.None;
            _amountLabel.text = "";
            _amountLabel.style.display = DisplayStyle.None;
            _durationLabel.text = "";
            _durationLabel.style.display = DisplayStyle.None;
            _cooldownOverlay.style.backgroundColor = new Color(0, 0, 0, 0);
            RemoveFromClassList("disabled");
        }

        public string GetItemId()
        {
            if (!string.IsNullOrEmpty(_itemId)) return _itemId;
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || InventoryEntity == Entity.Null) return "";

            var entityManager = world.EntityManager;
            if (!entityManager.HasBuffer<SlotData>(InventoryEntity)) return "";

            var slots = entityManager.GetBuffer<SlotData>(InventoryEntity);
            if (SlotIndex < 0 || SlotIndex >= slots.Length) return "";

            return slots[SlotIndex].DataId.ToString();
        }

        public int GetAmount()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || InventoryEntity == Entity.Null) return 0;

            var entityManager = world.EntityManager;
            if (!entityManager.HasBuffer<SlotData>(InventoryEntity)) return 0;

            var slots = entityManager.GetBuffer<SlotData>(InventoryEntity);
            if (SlotIndex < 0 || SlotIndex >= slots.Length) return 0;

            return slots[SlotIndex].Amount;
        }

        private Color GetQualityColor(string quality)
        {
            return quality switch
            {
                "rare" => new Color(0.2f, 0.4f, 0.8f, 1f),
                "uncommon" => new Color(0.2f, 0.7f, 0.3f, 1f),
                _ => new Color(0.3f, 0.3f, 0.3f, 1f)
            };
        }

        private string GetBindKey(int index)
        {
            return index switch
            {
                9 => "0",
                10 => "-",
                11 => "=",
                _ => (index + 1).ToString()
            };
        }
    }
}

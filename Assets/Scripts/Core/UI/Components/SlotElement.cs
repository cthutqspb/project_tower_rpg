using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class SlotElement : VisualElement, IDragSource
    {
        private VisualElement _icon;
        private VisualElement _cooldownOverlay;
        private Label _bindLabel;
        private Label _amountLabel;
        private Label _durationLabel;
        
        // Слот кэширует свои данные только для Drag-and-Drop
        private string _itemId = "";
        private int _amount = 0;

        public int SlotIndex { get; set; }
        public Entity ContainerEntity { get; set; }
        public string DataSourceId { get; set; }
        
        // Оставляем строки в UI для USS-стилей, это нормально
        public string GridType { get; set; } 

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

            var dragManipulator = new DragManipulator(this, DragMode.Slot);
            this.AddManipulator(dragManipulator);
        }

        // ================================================================
        // IDragSource Implementation
        // ================================================================
        
        public bool CanDrag() => !string.IsNullOrEmpty(_itemId) && _amount > 0;

        public DragData GetDragData()
        {
            return new DragData
            {
                Source = this,
                SlotIndex = SlotIndex, // Передаем чистый int
                ItemId = _itemId,
                Amount = _amount,
                Icon = null, // TODO: вытащить спрайт из конфигурации, если нужно
                SourceId = DataSourceId,
                GridType = GridType,
                SourceEntity = ContainerEntity
            };
        }

        // ================================================================
        // Public Methods (Слот теперь просто принимает готовые данные)
        // ================================================================

        public void SetData(string itemId, ItemConfig config, string gridType, int index, int amount = 1)
        {
            _itemId = itemId;
            _amount = amount;
            GridType = gridType;
            SlotIndex = index;
            
            ClearVisual();

            if (config == null || string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return; // Если слот пустой, просто выходим (он уже очистился внутри ClearVisual)
            }

            _icon.style.display = DisplayStyle.Flex;
            _icon.style.backgroundColor = GetQualityColor(config.identity.quality);

            if (gridType == "action_bar")
            {
                _bindLabel.style.display = DisplayStyle.Flex;
                _bindLabel.text = GetBindKey(index);
            }

            // Проверяем, что количество больше 1, чтобы отобразить счетчик стака
            if (amount > 1)
            {
                _amountLabel.style.display = DisplayStyle.Flex;
                _amountLabel.text = amount.ToString();
            }
            else
            {
                // Если 1 или меньше, скрываем лейбл количества
                _amountLabel.style.display = DisplayStyle.None;
                _amountLabel.text = "";
            }

        }

        // Метод Refresh() больше не лезет в ECS! Сетка сама обновит слот, когда прилетит буфер.
        public void Refresh() 
        {
            // Метод можно оставить пустым или убрать, так как обновление идёт через SetData
        }

        public void ClearVisual()
        {
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


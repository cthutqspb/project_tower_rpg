using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class ItemSplitWindow : UIWindow, IPayloadReceiver
    {
        public override WindowType Type => WindowType.ItemSplit;
        public override bool CanCloseByEscape => true;

        private SplitStackPayload _payload;
        private int _currentAmount;
        private int _maxSplittable;

        private VisualElement _header;
        private Label _itemIcon;
        private Label _itemName;
        private SliderInt _amountSlider; // 🎯 Заменено на SliderInt
        private Label _amountLabel;
        private Button _splitBtn, _cancelBtn;

        public void ReceivePayload(object payload)
        {
            if (payload is SplitStackPayload p)
            {
                _payload = p;
                _currentAmount = Mathf.Max(1, p.MaxAmount / 2);
                _maxSplittable = Mathf.Max(1, p.MaxAmount - 1);

                 // 🎯 Если окно уже открыто — просто обновляем данные, не вызывая Open() повторно
                if (!IsOpen)
                    Open();
                BuildUI();                
            }
        }

        protected override void OnWindowBuilt(VisualElement root)
        {
            _header = root.Q<VisualElement>("header");
            _itemIcon = root.Q<Label>("item-icon");
            _itemName = root.Q<Label>("item-name");
            
            // 🎯 Ищем SliderInt (убедись, что в UXML тоже SliderInt)
            _amountSlider = root.Q<SliderInt>("amount-slider"); 
            
            _amountLabel = root.Q<Label>("amount-label");
            _splitBtn = root.Q<Button>("confirm-btn");
            _cancelBtn = root.Q<Button>("cancel-btn");

            // Отключаем отображение текстового поля (оно все еще скрыто через USS)
            _amountSlider.showInputField = false;

            _splitBtn.clicked += OnConfirmClicked;
            _cancelBtn.clicked += Close;

            // 🎯 Слушаем изменения (SliderInt возвращает чистый int)
            _amountSlider.RegisterValueChangedCallback(evt =>
            {
                _currentAmount = evt.newValue;
                _amountLabel.text = $"{_currentAmount} / {_maxSplittable}";
            });

            if (_header != null)
            {
                var drag = new DragManipulator(_header, _window, DragMode.UIElement);
                _header.AddManipulator(drag);
            }
        }

        private void BuildUI()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (em.HasBuffer<ItemSlot>(_payload.SourceContainer))
            {
                var slots = em.GetBuffer<ItemSlot>(_payload.SourceContainer);
                if (_payload.SourceSlot >= 0 && _payload.SourceSlot < slots.Length)
                {
                    var slot = slots[_payload.SourceSlot];
                    var cfg = ItemsDatabase.GetItem(slot.DataId.ToString());
                    if (cfg != null)
                    {
                        _itemIcon.text = cfg.visuals?.icon_char ?? "?";
                        _itemName.text = LocalizationManager.Get(cfg.identity.name_key);
                    }
                }
            }

            _amountSlider.highValue = _maxSplittable;
            _amountSlider.value = _currentAmount;
            _amountLabel.text = $"{_currentAmount} / {_maxSplittable}";
        }

        private void OnConfirmClicked()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            string itemId = "";
            if (em.HasBuffer<ItemSlot>(_payload.SourceContainer))
            {
                var slots = em.GetBuffer<ItemSlot>(_payload.SourceContainer);
                if (_payload.SourceSlot >= 0 && _payload.SourceSlot < slots.Length)
                    itemId = slots[_payload.SourceSlot].DataId.ToString();
            }

            var iconData = new IconData
            {
                Glyph = _itemIcon.text,
                Color = _itemIcon.resolvedStyle.color,
                Classes = new List<string>(_itemIcon.GetClasses())
            };

            var dragData = new DragData
            {
                Source = null,
                SlotIndex = _payload.SourceSlot,
                ItemId = itemId,
                Amount = _currentAmount,
                Icon = iconData,
                SourceEntity = _payload.SourceContainer,
                TargetEntity = Entity.Null
            };

            Close();

            // 🎯 Отложенный старт: текущий PointerUp (клик по кнопке) спокойно
            // долетит до _root без _activeDrag, а на следующем тике ghost прилипнет к мыши.
            DragManager.Instance.StartDrag(dragData, captureOnRoot: true);
        }   
    }
}

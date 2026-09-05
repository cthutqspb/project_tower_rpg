using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.UI.Colors;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class SlotElement : VisualElement, IDragSource
    {
        // Было: private VisualElement _icon;
        private Label _icon; // ✅ Изменили на Label для поддержки текста-иконок Nerd Font
        private VisualElement _cooldownOverlay;
        private Label _bindLabel;
        private Label _amountLabel;
        private Label _durationLabel;
        
        // Слот кэширует свои данные только для Drag-and-Drop
        private string _itemId = "";
        private int _amount = 0;

        public int SlotIndex { get; set; }
        public Entity ContainerEntity { get; set; }
        
        // Оставляем строки в UI для USS-стилей, это нормально
        public string GridType { get; set; } 

        public SlotElement()
        {
            this.AddToClassList("slot");
            this.style.backgroundColor = SolarizedOsakaNight.Background;
            
            _icon = new Label(); // ✅ Создаем как текстовый Label
            _icon.AddToClassList("slot-icon");
            _icon.style.paddingLeft = 0;

            _icon.style.width = Length.Percent(100f);
            _icon.style.height = Length.Percent(100f);
            _icon.style.display = DisplayStyle.None;

            // 🦾 ФРОНТЕНД-ХАК: Идеально центрируем иконку-значок внутри ячейки
            _icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _icon.style.fontSize = 39; // Оптимальный размер для Nerd Font глифов в ячейке 40х40
            _icon.style.color = new Color(187, 154, 247, 1);

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

            this.RegisterCallback<PointerOverEvent>(OnPointerOver);
            this.RegisterCallback<PointerOutEvent>(OnPointerOut);
            this.RegisterCallback<PointerDownEvent>(OnSlotClicked);

            var dragManipulator = new DragManipulator(this, DragMode.Slot);
            this.AddManipulator(dragManipulator);

            UIEvents.OnFlashSlot += FlashSlot;
        }

        ~SlotElement()
        {
            UIEvents.OnFlashSlot -= FlashSlot;
        }

        /// <summary>
        /// Универсальный метод вспышки ячейки (Wow-канон)
        /// </summary>
        public void FlashSlot(int targetGlobalIndex)
        {
            // Каждая ячейка на экране сама проверяет Си-паспорт. Не совпало — молча выходим!
            if (this.SlotIndex != targetGlobalIndex) return;

            // Сочно ужимаем и подсвечиваем рамку по нативному USS-классу
            this.AddToClassList("slot-active-flash");

            // Ровно через 100 мс стираем класс, возвращая анимацию сжатия назад
            this.schedule.Execute(() => 
            {
                this.RemoveFromClassList("slot-active-flash");
            }).ExecuteLater(100);
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
                SlotIndex = SlotIndex,
                ItemId = _itemId,
                Amount = _amount,
                Icon = new IconData
                {
                    Glyph = _icon.text,
                    Color = _icon.style.color.value,
                    FontSize = 39f
                },
                SourceEntity = ContainerEntity
            };
        }

        // ================================================================
        // Public Methods (Слот теперь просто принимает готовые данные)
        // ================================================================

        /// <summary>
        /// Полная установка данных слота (иконка, количество, бинд, валидация)
        /// </summary>
        public void SetData(
            string itemId,
            int index,
            int amount = 1,
            bool isActionBar = false,
            string bindingText = "",
            CastValidationResult validation = default)
        {
            _itemId = itemId;
            _amount = amount;
            SlotIndex = index;
            ClearVisual();

            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            _icon.style.display = DisplayStyle.Flex;

            string quality = "common";
            string iconCharacter = "";

            var abilityConfig = AbilitiesDatabase.GetAbility(itemId);
            if (abilityConfig != null)
            {
                quality = abilityConfig.identity?.@class ?? "common";
                //_icon.style.backgroundColor = SolarizedOsakaNight.GetAbilityColor(quality);
                _icon.style.color = SolarizedOsakaNight.GetAbilityColor(quality);
                _icon.style.borderTopColor = SolarizedOsakaNight.GetAbilityColor(quality);
                _icon.style.borderRightColor = SolarizedOsakaNight.GetAbilityColor(quality);
                _icon.style.borderBottomColor = SolarizedOsakaNight.GetAbilityColor(quality);
                _icon.style.borderLeftColor = SolarizedOsakaNight.GetAbilityColor(quality);
                
                iconCharacter = abilityConfig.visuals?.icon_char ?? "";
            }
            else
            {
                var itemConfig = ItemsDatabase.GetItem(itemId);
                if (itemConfig != null)
                {
                    quality = itemConfig.identity?.quality ?? "common";
                    //_icon.style.backgroundColor = SolarizedOsakaNight.GetQualityColor(quality);
                    _icon.style.color = SolarizedOsakaNight.GetQualityColor(quality);
                    _icon.style.borderTopColor = SolarizedOsakaNight.GetQualityColor(quality);
                    _icon.style.borderRightColor = SolarizedOsakaNight.GetQualityColor(quality);
                    _icon.style.borderBottomColor = SolarizedOsakaNight.GetQualityColor(quality);
                    _icon.style.borderLeftColor = SolarizedOsakaNight.GetQualityColor(quality);
                    iconCharacter = itemConfig.visuals?.icon_char ?? "";
                }
            }
 
            _icon.text = iconCharacter;

            // Количество (для стаков)
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

            // Экшен-бар: бинд-клавиша и валидация
            if (isActionBar)
            {
                _bindLabel.text = bindingText;
                _bindLabel.style.color = SolarizedOsakaNight.Text;
                _icon.style.opacity = 1.0f;

                ApplyValidation(validation);
            }
            else
            {
                _bindLabel.style.display = DisplayStyle.None;
                _bindLabel.text = "";
                _bindLabel.style.color = SolarizedOsakaNight.Text;
                _icon.style.opacity = 1.0f;
                ResetValidationStyle();
            }
        }

        /// <summary>
        /// Только обновление валидации (цвет, прозрачность) — ЛЁГКИЙ МЕТОД!
        /// </summary>
        public void SetValidation(CastValidationResult validation)
        {
            // Если это не экшен-бар — выходим
            if (_bindLabel.style.display == DisplayStyle.None) return;

            // Сбрасываем стили перед применением новых
            ResetValidationStyle();

            if (!validation.IsPossible && !string.IsNullOrEmpty(validation.Reason))
            {
                ApplyValidation(validation);
            }
        }

        /// <summary>
        /// Применяет визуальные эффекты валидации
        /// </summary>
        private void ApplyValidation(CastValidationResult validation)
        {
            switch (validation.Reason)
            {
                case "OUT_OF_RANGE":
                    _bindLabel.style.color = Color.red;
                     var currentColor = _icon.style.color.value;
                    _icon.style.color = currentColor.Darken(0.4f).Desaturate(0.6f);
                    _bindLabel.style.color = SolarizedOsakaNight.Red;
                    break;

                case "NO_MANA":
                    _icon.style.opacity = 0.4f;
                    _bindLabel.style.color = SolarizedOsakaNight.Red;
                    break;

                case "INVALID_TARGET":
                case "NO_TARGET":
                    _bindLabel.style.color = SolarizedOsakaNight.Red;
                    break;
            }
        }

        /// <summary>
        /// Сбрасывает стили валидации
        /// </summary>
        private void ResetValidationStyle()
        {
            _icon.style.backgroundColor = Color.clear; 
            _icon.style.opacity = 1.0f;
            _bindLabel.style.color = Color.white;
        }

        // Метод Refresh() больше не лезет в ECS! Сетка сама обновит слот, когда прилетит буфер.
        public void Refresh() 
        {
            // Метод можно оставить пустым или убрать, так как обновление идёт через SetData
        }

        public void ClearVisual()
        {
            _icon.text = ""; // ✅ Очищаем символ-значок Nerd Font
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
            ResetValidationStyle();
        }

        private void OnSlotClicked(PointerDownEvent evt)
        {
            if (string.IsNullOrEmpty(_itemId)) return;

            // Создаем универсальный контекст, куда пишем ВСЕ параметры мыши
            var context = new UiClickContext
            {
                ContextEntity = this.ContainerEntity,
                SlotIndex = this.SlotIndex,
                TargetId = _itemId,
                Amount = _amount,
                MousePosition = evt.position,
                MouseButton = evt.button, // 0 или 1
                ClickCount = evt.clickCount // 1 или 2
            };

            // Отправляем в единый канал
            UIEvents.TriggerUiClick(context);
            evt.StopPropagation();
        }
        
        private void OnPointerOver(PointerOverEvent evt)
        {
            //Debug.Log($"[SlotElement] Мышь ХОВЕР на слоте #{SlotIndex}. Текущий ID: '{_itemId}'");

            if (string.IsNullOrEmpty(_itemId)) 
            {
                //Debug.Log($"[SlotElement] Слот #{SlotIndex} пустой, сессия тултипа пропущена.");
                return;
            }

            // 1. Сначала пытаемся найти шмотку в базе предметов
            var itemConfig = ItemsDatabase.GetItem(_itemId);
            if (itemConfig != null)
            {
                //Debug.Log($"[SlotElement] УСПЕХ! Найдена шмотка '{_itemId}'. Запускаем TooltipManager.");
                TooltipManager.Show(TooltipDomain.INTERFACE, TooltipKind.ITEM, itemConfig);
                return; // Выходим, тултип шмотки показан
            }

            // 2. ✅ Если шмотки нет — проверяем, вдруг это Способность (Spell/Ability) из новой базы!
            var abilityConfig = AbilitiesDatabase.GetAbility(_itemId);
            if (abilityConfig != null)
            {
                //Debug.Log($"[SlotElement] УСПЕХ! Найдена способность '{_itemId}'. Запускаем TooltipManager.");
                
                // Передаем в твой TooltipManager домен интерфейса, тип абилки и сам конфиг способности
                TooltipManager.Show(TooltipDomain.INTERFACE, TooltipKind.ABILITY, abilityConfig);
            }
            else
            {
                // Если вообще нигде нет — значит реальная ошибка данных
                //Debug.LogError($"[SlotElement] КРИТИЧЕСКАЯ ОШИБКА: ID '{_itemId}' прописан в слоте, но его НЕТ ни в ItemsDatabase, ни в AbilitiesDatabase!");
            }
        }


        private void OnPointerOut(PointerOutEvent evt)
        {
            //Debug.Log($"[SlotElement] Мышь УШЛА со слота #{SlotIndex}. Гасим тултип.");
            TooltipManager.HideGuiTooltips();
        }

    }
}

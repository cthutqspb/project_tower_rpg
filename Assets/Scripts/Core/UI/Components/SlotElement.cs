using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using System.Linq;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.UI.Colors;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class SlotElement : VisualElement, IDragSource
    {
        // Было: private VisualElement _icon;
        private Label _icon; // ✅ Изменили на Label для поддержки текста-иконок Nerd Font 
        private Label _bindLabel;
        private Label _amountLabel;
        private Label _durationLabel;

        private VisualElement _cooldownOverlay;
        private float _gcdProgress = 0f;
        
        // Слот кэширует свои данные только для Drag-and-Drop
        private string _itemId = "";
        private int _amount = 0;
        
        private bool _isActionBarSlot = false;

        public int SlotIndex { get; set; }
        public Entity ContainerEntity { get; set; }

        public SlotElement()
        {
            this.AddToClassList("slot");
            this.style.backgroundColor = SolarizedOsakaNight.Background;
            this.style.overflow = Overflow.Hidden;
            
            _icon = new Label(); // ✅ Создаем как текстовый Label
            _icon.AddToClassList("slot-icon");
            _icon.style.display = DisplayStyle.None;
            Add(_icon);
            
            _cooldownOverlay = new VisualElement();
            _cooldownOverlay.AddToClassList("slot-cooldown-overlay");
            //_cooldownOverlay.style.backgroundColor = new Color(0, 0, 0, 0);
            _cooldownOverlay.generateVisualContent += DrawRadialCooldown;
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
            if (!_isActionBarSlot) return;
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
                    Classes = _icon.GetClasses().ToList()
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
            CastValidationResult validation = default
        )
        {
            _itemId = itemId;
            _amount = amount;
            _isActionBarSlot = isActionBar;
            SlotIndex = index;
            ClearVisual();

            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            _icon.style.display = DisplayStyle.Flex;

            string iconCharacter = "";

            var abilityConfig = AbilitiesDatabase.GetAbility(itemId);
            if (abilityConfig != null)
            {
                SetAbilityClass(abilityConfig.identity?.@class ?? "");                               
                iconCharacter = abilityConfig.visuals?.icon_char ?? "";
            }
            else
            {
                var itemConfig = ItemsDatabase.GetItem(itemId);
                if (itemConfig != null)
                {
                    SetItemQualityClass(itemConfig.identity?.quality ?? "common");
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
                _bindLabel.style.display = DisplayStyle.Flex;
                _bindLabel.text = bindingText;
                _bindLabel.style.color = SolarizedOsakaNight.Yellow;
                //_icon.style.opacity = 1.0f;

                ApplyValidation(validation);
            }
            else
            {
                _bindLabel.style.display = DisplayStyle.None;
                _bindLabel.text = "";
                _bindLabel.style.color = SolarizedOsakaNight.Yellow;
                //_icon.style.opacity = 1.0f;
                ResetValidationStyle();
            }
        }

        private void SetAbilityClass(string classType)
        {
            // Удаляем старый класс
            _icon.RemoveFromClassList("ability-mage");
            _icon.RemoveFromClassList("ability-warrior");
            _icon.RemoveFromClassList("ability-rogue");
            _icon.RemoveFromClassList("ability-priest");
            
            // Добавляем новый
            string abilityClass = classType?.ToLower() switch
            {
                "mage" => "ability-mage",
                "warrior" => "ability-warrior",
                "rogue" => "ability-rogue",
                "priest" => "ability-priest",
                _ => "ability-default"
            };
            
            if (!string.IsNullOrEmpty(abilityClass))
                _icon.AddToClassList(abilityClass);
        }

        private void SetItemQualityClass(string quality)
        {
            // Удаляем старые классы
            _icon.RemoveFromClassList("item-common");
            _icon.RemoveFromClassList("item-uncommon");
            _icon.RemoveFromClassList("item-rare");
            _icon.RemoveFromClassList("item-epic");
            _icon.RemoveFromClassList("item-legendary");
            _icon.RemoveFromClassList("item-artifact");
            
            // Добавляем новый
            string qualityClass = quality?.ToLower() switch
            {
                "rare" => "item-rare",
                "uncommon" => "item-uncommon",
                "epic" => "item-epic",
                "legendary" => "item-legendary",
                "artifact" => "item-artifact",
                _ => "item-common"
            };
            
            if (!string.IsNullOrEmpty(qualityClass))
                _icon.AddToClassList(qualityClass);
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

        public void SetGlobalCooldown(float gcdRemaining, float gcdDuration) 
    {
        // 1. Быстрая проверка на отсутствие кулдауна
        if (gcdRemaining <= 0f || gcdDuration <= 0f)
        {
            _gcdProgress = 0f;
            _cooldownOverlay.MarkDirtyRepaint(); // Очищаем круг
            return;
        }

        // 2. Тупо пишем прогресс (0.0..1.0) напрямую из аргументов вашей ECS-системы
        _gcdProgress = Mathf.Clamp01(gcdRemaining / gcdDuration);
        
        // 3. Пингуем UI Toolkit, что геометрию пора обновить на этом кадре
        _cooldownOverlay.MarkDirtyRepaint();
    }

        private void DrawRadialCooldown(MeshGenerationContext context)
    {
        // Если кулдауна нет — ничего не рисуем (оверлей полностью прозрачный)
        if (_gcdProgress <= 0f) return;
        
        var painter = context.painter2D;
        painter.fillColor = new Color(0f, 0f, 0f, 0.6f); // 60% затемнения
        
        Rect rect = _cooldownOverlay.contentRect;
        Vector2 center = rect.center;
        
        // Считаем радиус с запасом под углы квадратной кнопки
        float radius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f;
        
        float startAngle = -90f; // 12 часов дня
        float sweepAngle = _gcdProgress * 360f; // Сектор уменьшается по часовой стрелке
        
        painter.BeginPath();
        painter.MoveTo(center);
        painter.Arc(center, radius, startAngle, startAngle + sweepAngle);
        painter.LineTo(center);
        painter.Fill();
    }
        /// <summary>
        /// Применяет визуальные эффекты валидации
        /// </summary>
        private void ApplyValidation(CastValidationResult validation)
        {
            switch (validation.Reason)
            {
                case "OUT_OF_RANGE":
                    _icon.AddToClassList("slot-icon-out-of-range");
                    _bindLabel.style.color = SolarizedOsakaNight.Red;
                    break;

                case "NO_MANA":
                    _icon.AddToClassList("slot-icon-no-mana");
                    _bindLabel.style.color = SolarizedOsakaNight.Blue;
                    break;

                case "INVALID_TARGET":
                case "NO_TARGET":
                    _bindLabel.style.color = SolarizedOsakaNight.Red;
                    _icon.AddToClassList("slot-icon-invalid-target");
                    break;
            }
        }

        /// <summary>
        /// Сбрасывает стили валидации
        /// </summary>
        private void ResetValidationStyle()
        {
            _icon.RemoveFromClassList("slot-icon-out-of-range");
            _icon.RemoveFromClassList("slot-icon-no-mana");
            _icon.RemoveFromClassList("slot-icon-invalid-target");
            
            _icon.style.backgroundColor = Color.clear;
            //_icon.style.opacity = 1.0f;
            _bindLabel.style.color = Color.yellow;
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
            //_cooldownOverlay.style.backgroundColor = new Color(0, 0, 0, 0);
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

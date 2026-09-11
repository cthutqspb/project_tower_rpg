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

        private VisualElement _flashOverlay;
        private VisualElement _cooldownOverlay;

        private float _gcdProgress = 0f;
        private float _flashProgress = 0f;
        private float _flashAngle = 0f;
        
        // Слот кэширует свои данные только для Drag-and-Drop
        private string _itemId = "";
        private int _amount = 0;
        
        private bool _isActionBarSlot = false;

        public int SlotIndex { get; set; }
        public Entity ContainerEntity { get; set; }
        
        private static readonly string[] AbilityClasses = { 
            "ability-mage",
            "ability-warrior",
            "ability-rogue",
            "ability-priest",
            "ability-default"
        };
        private static readonly string[] QualityClasses = {
            "item-common",
            "item-uncommon",
            "item-rare",
            "item-epic",
            "item-legendary",
            "item-artifact"
        };

        public SlotElement()
        {
            this.AddToClassList("slot");
            this.style.backgroundColor = SolarizedOsakaNight.Background;
            this.style.overflow = Overflow.Hidden;
            
            _icon = new Label(); // ✅ Создаем как текстовый Label
            _icon.AddToClassList("slot-icon");
            Add(_icon);
            
            _cooldownOverlay = new VisualElement();
            _cooldownOverlay.AddToClassList("slot-cooldown-overlay");
            _cooldownOverlay.generateVisualContent += DrawRadialCooldown;
            Add(_cooldownOverlay);
            
            _bindLabel = new Label();
            _bindLabel.AddToClassList("slot-bind-label");
            Add(_bindLabel);
            
            _amountLabel = new Label();
            _amountLabel.AddToClassList("slot-amount-label");
            Add(_amountLabel);
            
            _durationLabel = new Label();
            _durationLabel.AddToClassList("slot-duration-label");
            Add(_durationLabel);

            this.RegisterCallback<PointerOverEvent>(OnPointerOver);
            this.RegisterCallback<PointerOutEvent>(OnPointerOut);
            this.RegisterCallback<PointerDownEvent>(OnSlotClicked);

            var dragManipulator = new DragManipulator(this, DragMode.Slot);
            this.AddManipulator(dragManipulator);
            
            this.RegisterCallback<AttachToPanelEvent>(OnAttach);
            this.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            // Подписываемся на ивент анимаций ТОЛЬКО когда слот физически появился на экране
            UIEvents.OnSlotAnimation += AnimateSlot;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            // Намертво отписываемся в тот же миг, как слот удален из панели. Нулевой Memory Leak!
            UIEvents.OnSlotAnimation -= AnimateSlot;
        }

        /// <summary>
        /// Универсальный метод вспышки ячейки (Wow-канон)
        /// </summary>
        public void AnimateSlot(int targetGlobalIndex, SlotAnimationType animationType)
        {   
            if (!_isActionBarSlot) return;
            // Каждая ячейка на экране сама проверяет Си-паспорт. Не совпало — молча выходим!
            if (this.SlotIndex != targetGlobalIndex) return;
            
            switch (animationType)
            {
                case SlotAnimationType.Press:
                    this.AddToClassList("slot-active-flash");
                    // Ровно через 100 мс стираем класс, возвращая анимацию сжатия назад
                    this.schedule.Execute(() => 
                    {
                        this.RemoveFromClassList("slot-active-flash");
                    }).ExecuteLater(100);
                    break;

                case SlotAnimationType.CooldownReady:
                    _flashProgress = 0.01f;
                    _flashAngle = 0f;
                    
                    int totalSteps = 32; // ✅ 30 кадров = ~500 мс (идеально для WC3-вспышки)
                    int currentStep = 0;

                    this.schedule.Execute(() => 
                    {
                        currentStep++;
                        float linearT = (float)currentStep / totalSteps;

                        // Взрывной еазинг радиуса звезды (Cubic)
                        _flashProgress = 1f - Mathf.Pow(1f - linearT, 3f); 
                        
                        // 🌀 КРУЧЕНИЕ: звезда делает 120 градусов за время вспышки (более динамично)
                        _flashAngle = _flashProgress * 120f; 

                        _cooldownOverlay.MarkDirtyRepaint();

                        if (currentStep >= totalSteps)
                        {
                            _flashProgress = 0f;
                            _flashAngle = 0f;
                            _cooldownOverlay.MarkDirtyRepaint();
                        }
                    }).Every(16).Until(() => currentStep >= totalSteps);
                    break;



                case SlotAnimationType.Proc:
                    // В будущем: включить золотую обводку
                    break;

                case SlotAnimationType.Warning:
                    // В будущем: моргнуть красной рамкой
                    break;
            }
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

            _icon.RemoveFromClassList("hidden");
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
                _amountLabel.RemoveFromClassList("hidden");
                _amountLabel.text = amount.ToString();
            }
            else
            {
                _amountLabel.AddToClassList("hidden");
                _amountLabel.text = "";
            }

            // Экшен-бар: бинд-клавиша и валидация
            if (isActionBar)
            {
                _bindLabel.RemoveFromClassList("hidden");
                _bindLabel.text = bindingText;
                //_bindLabel.style.color = SolarizedOsakaNight.Yellow;
                //_icon.style.opacity = 1.0f;

                ApplyValidation(validation);
            }
            else
            {
                _bindLabel.AddToClassList("hidden");
                _bindLabel.text = "";
                //_bindLabel.style.color = SolarizedOsakaNight.Yellow;
                //_icon.style.opacity = 1.0f;
                ResetValidationStyle();
            }
        }

        private void SetAbilityClass(string classType)
        {
            // Удаляем старый класс
            foreach (var cls in AbilityClasses)
                _icon.RemoveFromClassList(cls);
            
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
            foreach (var cls in QualityClasses)
                _icon.RemoveFromClassList(cls);
            
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
            if (_bindLabel.ClassListContains("hidden")) return;

            // Сбрасываем стили перед применением новых
            ResetValidationStyle();

            if (!validation.IsPossible && !string.IsNullOrEmpty(validation.Reason))
            {
                ApplyValidation(validation);
            }
        }

        public void SetCooldown(float remaining, float duration, bool isGlobalCooldown) 
        {
            // 1. Быстрая проверка на отсутствие кулдауна
            if (remaining <= 0f || duration <= 0f)
            {
                _gcdProgress = 0f;
                _cooldownOverlay.MarkDirtyRepaint(); // Очищаем круг
                return;
            }

            // 2. Рассчитываем базовый прогресс (от 0.0 до 1.0)
            float rawProgress = Mathf.Clamp01(remaining / duration);

            // 3. Дифференцируем направление по WoW-канону:
            // ГКД уходит назад (против часовой), обычный КД уходит вперед (по часовой)
            _gcdProgress = isGlobalCooldown ? rawProgress : (1f - rawProgress);
            
            // 4. Пингуем UI Toolkit, что геометрию пора обновить на этом кадре
            _cooldownOverlay.MarkDirtyRepaint();
        }

        private void DrawRadialCooldown(MeshGenerationContext context)
        {
            // 🚀 Быстрый выход, если ничего не рисуем
            if (_gcdProgress <= 0f && _flashProgress <= 0f) return;

            var painter = context.painter2D;
            painter.fillGradient = default;
            Rect rect = _cooldownOverlay.contentRect;
            Vector2 center = rect.center;
            float baseRadius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f;

            // =========================================================================
            // СЛОЙ 1: GCD (чёрный сектор)
            // =========================================================================
            if (_gcdProgress > 0f)
            {
                painter.fillColor = new Color(0f, 0f, 0f, 0.72f);
                float startAngle = -90f;
                float sweepAngle = _gcdProgress * 360f;

                painter.BeginPath();
                painter.MoveTo(center);
                painter.Arc(center, baseRadius, startAngle, startAngle + sweepAngle);
                painter.LineTo(center);
                painter.Fill();
            }

            // =========================================================================
            // СЛОЙ 2: ВСПЫШКА (один слой с градиентом!)
            // =========================================================================
            if (_flashProgress > 0f)
            {
                float alpha = Mathf.Pow(1f - _flashProgress, 1.5f);
                float size = baseRadius * Mathf.Lerp(0.1f, 2.0f, _flashProgress);
                float width = size * 0.15f;

                float rad = _flashAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                Vector2 Rotate(float x, float y) => new Vector2(
                    center.x + (x * cos - y * sin),
                    center.y + (x * sin + y * cos)
                );

                // 🔥 ОДИН ПРОХОД РИСОВАНИЯ (звезда + ядро + свечение)
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f;
                    float radAngle = angle * Mathf.Deg2Rad;
                    float c = Mathf.Cos(radAngle);
                    float s = Mathf.Sin(radAngle);

                    Vector2 tip = Rotate(c * size, s * size);
                    Vector2 left = Rotate(c * width - s * width * 0.3f, s * width + c * width * 0.3f);
                    Vector2 right = Rotate(c * width + s * width * 0.3f, s * width - c * width * 0.3f);

                    // ОСНОВНАЯ ЗВЕЗДА
                    painter.fillColor = new Color(0.9f, 0.95f, 1f, alpha * 0.85f);
                    painter.BeginPath();
                    painter.MoveTo(center);
                    painter.QuadraticCurveTo(left, tip);
                    painter.QuadraticCurveTo(right, center);
                    painter.Fill();
                }

                // ЯДРО (отдельно, но быстро)
                float coreSize = size * Mathf.Lerp(0.05f, 0.15f, 1f - _flashProgress);
                painter.fillColor = new Color(1f, 1f, 1f, alpha * 0.95f);
                painter.BeginPath();
                painter.Arc(center, coreSize, 0f, 360f);
                painter.Fill();

                // ХВОСТЫ (тонкие, один проход)
                float trailLen = size * Mathf.Lerp(0.2f, 1.5f, 1f - _flashProgress);
                float trailWidth = width * 0.2f;

                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f + _flashAngle * 0.3f;
                    float radAngle = angle * Mathf.Deg2Rad;
                    float c = Mathf.Cos(radAngle);
                    float s = Mathf.Sin(radAngle);

                    Vector2 start = center + new Vector2(c * trailLen * 0.2f, s * trailLen * 0.2f);
                    Vector2 end = center + new Vector2(c * trailLen, s * trailLen);
                    Vector2 perp = new Vector2(-s * trailWidth * 0.5f, c * trailWidth * 0.5f);

                    painter.fillColor = new Color(0.3f, 0.6f, 1f, alpha * 0.2f);
                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.LineTo(end + perp);
                    painter.LineTo(end - perp);
                    painter.ClosePath();
                    painter.Fill();
                }
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
                    _icon.AddToClassList("slot-icon-out-of-range");
                    _bindLabel.AddToClassList("slot-bind-label-out-of-range");
                    break;

                case "NO_MANA":
                    _icon.AddToClassList("slot-icon-no-mana");
                    _bindLabel.AddToClassList("slot-bind-label-no-mana");
                    break;

                case "INVALID_TARGET":
                case "NO_TARGET":
                case "GCD_ACTIVE":
                    _icon.AddToClassList("slot-icon-invalid-target");
                    _bindLabel.AddToClassList("slot-bind-label-invalid-target");
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
            
            //_icon.style.backgroundColor = Color.clear;
            //_icon.style.opacity = 1.0f;
            _bindLabel.RemoveFromClassList("slot-bind-label-out-of-range");
            _bindLabel.RemoveFromClassList("slot-bind-label-no-mana");
            _bindLabel.RemoveFromClassList("slot-bind-label-invalid-target");
        }

        // Метод Refresh() больше не лезет в ECS! Сетка сама обновит слот, когда прилетит буфер.
        public void Refresh() 
        {
            // Метод можно оставить пустым или убрать, так как обновление идёт через SetData
        }

        public void ClearVisual()
        {
            foreach (var cls in AbilityClasses) _icon.RemoveFromClassList(cls);
            foreach (var cls in QualityClasses) _icon.RemoveFromClassList(cls);
            _icon.text = ""; // ✅ Очищаем символ-значок Nerd Font
            _icon.AddToClassList("hidden");
            //_icon.style.backgroundColor = Color.clear;
            _bindLabel.text = "";
            _bindLabel.AddToClassList("hidden");
            _amountLabel.text = "";
            _amountLabel.AddToClassList("hidden");
            _durationLabel.text = "";
            _durationLabel.AddToClassList("hidden");
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

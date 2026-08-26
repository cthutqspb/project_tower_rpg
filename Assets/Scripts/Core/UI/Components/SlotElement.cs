using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.ECS.Components;

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
            
            //_icon = new VisualElement();
            _icon = new Label(); // ✅ Создаем как текстовый Label
            _icon.AddToClassList("slot-icon");
            _icon.style.width = 40;
            _icon.style.height = 40;
            _icon.style.display = DisplayStyle.None;

            // 🦾 ФРОНТЕНД-ХАК: Идеально центрируем иконку-значок внутри ячейки
            _icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _icon.style.fontSize = 24; // Оптимальный размер для Nerd Font глифов в ячейке 40х40
            
            // Загружаем наш сгенерированный TMP Font Asset из папки Resources
            // (Не забудь положить TerminessNerdFontMono-Regular SDF.asset в Assets/Resources/Fonts/)
            // var nerdFont = Resources.Load<Font>("Fonts/TerminessNerdFontMono-Regular SDF");
            // if (nerdFont != null)
            // {
            //     _icon.style.unityFontDefinition = new StyleFontDefinition(nerdFont);
            // }

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
                SourceEntity = ContainerEntity
            };
        }

        // ================================================================
        // Public Methods (Слот теперь просто принимает готовые данные)
        // ================================================================

        public void SetData(string itemId, ItemConfig config, int index, int amount = 1)
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

            if (config != null)
            {
                quality = config.identity.quality;
            }
            else
            {
                var abilityConfig = AbilitiesDatabase.GetAbility(itemId);
                if (abilityConfig != null)
                {
                    iconCharacter = abilityConfig.visuals.icon_char;
                }
            }

            _icon.style.backgroundColor = GetQualityColor(quality);
            _icon.text = iconCharacter;

            if (ContainerEntity != Entity.Null)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;

                bool isActionBar = em.HasComponent<ActionBarTag>(ContainerEntity);
                bool isAuraFrame = em.HasComponent<AuraFrameTag>(ContainerEntity);
                bool isInventory = em.HasComponent<InventoryTag>(ContainerEntity);
                bool isPaperdoll = em.HasComponent<PaperdollTag>(ContainerEntity);
                bool isContainer = em.HasComponent<ContainerTag>(ContainerEntity);

                if (isActionBar)
                {
                    _bindLabel.style.display = DisplayStyle.Flex;
                    _bindLabel.text = GetBindKey(index);
                }

                if (isAuraFrame)
                {
                    // TODO: отображать длительность ауры
                }
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
        
        private void OnPointerOver(PointerOverEvent evt)
        {
            //Debug.Log($"[SlotElement] Мышь ХОВЕР на слоте #{SlotIndex}. Текущий ID: '{_itemId}', GridType: '{GridType}'");

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


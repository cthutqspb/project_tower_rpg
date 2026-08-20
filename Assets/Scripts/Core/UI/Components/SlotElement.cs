using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.UI;
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

            this.RegisterCallback<PointerOverEvent>(OnPointerOver);
            this.RegisterCallback<PointerOutEvent>(OnPointerOut);

            var dragManipulator = new DragManipulator(this, DragMode.Slot);
            this.AddManipulator(dragManipulator);
            this.RegisterCallback<ClickEvent>(OnSlotClicked);
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

            if (config == null || string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            _icon.style.display = DisplayStyle.Flex;
            _icon.style.backgroundColor = GetQualityColor(config.identity.quality);

            // 🔥 ОПРЕДЕЛЯЕМ ТИП ПО ТЕГАМ (через ContainerEntity)
            if (ContainerEntity != Entity.Null)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                
                // Проверяем теги прямо в ECS
                bool isActionBar = em.HasComponent<ActionBarTag>(ContainerEntity);
                bool isAuraFrame = em.HasComponent<AuraFrameTag>(ContainerEntity);
                bool isInventory = em.HasComponent<InventoryTag>(ContainerEntity);
                bool isPaperdoll = em.HasComponent<PaperdollTag>(ContainerEntity);
                bool isContainer = em.HasComponent<ContainerTag>(ContainerEntity);

                // ================================================================
                // КЕЙС: ACTION BAR → показываем бинд-клавишу
                // ================================================================
                if (isActionBar)
                {
                    _bindLabel.style.display = DisplayStyle.Flex;
                    _bindLabel.text = GetBindKey(index);
                }

                // ================================================================
                // КЕЙС: АУРА → показываем длительность (задел)
                // ================================================================
                if (isAuraFrame)
                {
                    // TODO: отображать длительность ауры
                    // _durationLabel.style.display = DisplayStyle.Flex;
                    // _durationLabel.text = duration.ToString();
                }

                // ================================================================
                // КЕЙС: ИНВЕНТАРЬ, КУКЛА, КОНТЕЙНЕР → стандартное отображение
                // ================================================================
                // Ничего дополнительного не делаем, просто показываем иконку и количество
            }

            // Количество
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

        private void OnSlotClicked(ClickEvent evt)
        {
            if (string.IsNullOrEmpty(_itemId)) return;

            // Даблклик ЛКМ
            if (evt.clickCount == 2 && evt.button == 0)
            {
                UIEvents.TriggerSlotDoubleClick(this, SlotIndex, _itemId, _amount);
                evt.StopPropagation();
                return;
            }

            // ПКМ
            if (evt.clickCount == 1 && evt.button == 1)
            {
                // ✅ ИСПРАВЛЕНО: используем evt.position
                UIEvents.TriggerSlotRightClick(this, SlotIndex, _itemId, _amount, evt.position);
                evt.StopPropagation();
                return;
            }
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
            // ПРИНТ 1: Проверяем, реагирует ли вообще ячейка на мышь
            Debug.Log($"[SlotElement] Мышь ХОВЕР на слоте #{SlotIndex}. Текущий ItemId: '{_itemId}', GridType: '{GridType}'");

            if (string.IsNullOrEmpty(_itemId)) 
            {
                Debug.Log($"[SlotElement] Слот #{SlotIndex} пустой, сессия тултипа пропущена.");
                return;
            }

            var config = ItemsDatabase.GetItem(_itemId);
            if (config != null)
            {
                // ПРИНТ 2: База данных успешно нашла шмотку по ID
                Debug.Log($"[SlotElement] УСПЕХ! База данных нашла конфиг для '{_itemId}'. Название из конфига: '{config.identity.name_key}'. Запускаем TooltipManager.");
                
                TooltipManager.Show(TooltipDomain.INTERFACE, TooltipKind.ITEM, config);
            }
            else
            {
                // ПРИНТ 3: Ошибка в базе данных
                Debug.LogError($"[SlotElement] КРИТИЧЕСКАЯ ОШИБКА: Предмет '{_itemId}' прописан в слоте, но в ItemsDatabase его НЕТ!");
            }
        }

        private void OnPointerOut(PointerOutEvent evt)
        {
            Debug.Log($"[SlotElement] Мышь УШЛА со слота #{SlotIndex}. Гасим тултип.");
            TooltipManager.HideGuiTooltips();
        }

    }
}


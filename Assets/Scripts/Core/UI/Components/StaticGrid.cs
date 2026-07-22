using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement
    {
        private string _gridType;
        private int _columns;
        private int _rows;
        
        private List<string> _dataSource; 
        private List<GridSlotNodeCache> _slots = new(); // Храним чистый кэш нод ячеек!
        
        private bool _isDirty = false;

        public string GridType => _gridType;
        public bool IsShiftPressed { get; private set; }

        public StaticGrid(int columns, int rows, string gridType)
        {
            _columns = columns;
            _rows = rows;
            _gridType = gridType;
            
            this.AddToClassList("static-grid-container");
            this.AddToClassList($"grid-{_gridType}"); // Мутирует в "grid-inventory", "grid-action_bar" и т.д.

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.flexShrink = 0;
            style.flexGrow = 0;
            style.width = _columns * 48;

            // Выкачиваем ассеты ячейки со SSD
            VisualTreeAsset slotTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Components/SlotElement.uxml");
            StyleSheet slotStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Components/SlotElement.uss");

            if (slotTemplate == null)
            {
                Debug.LogError("🚨 [StaticGrid]: Не удалось найти SlotElement.uxml!");
                return;
            }

            int totalSlots = _columns * _rows;
            for (int i = 0; i < totalSlots; i++)
            {
                // Клонируем UXML дерево одной ячейки (Вместо gui.clone_tree)
                VisualElement slotRootInstance = slotTemplate.CloneTree();
                if (slotStyles != null) slotRootInstance.styleSheets.Add(slotStyles);

                // Находим ноды внутри клонированного дерева и пакуем в GridSlotNodeCache!
                GridSlotNodeCache slotCache = new GridSlotNodeCache
                {
                    Root = slotRootInstance.Q<VisualElement>(className: "base-slot") ?? slotRootInstance,
                    Icon = slotRootInstance.Q<VisualElement>(className: "slot-icon"),
                    GcdOverlay = slotRootInstance.Q<VisualElement>(className: "slot-cooldown-overlay"),
                    Amount = slotRootInstance.Q<Label>(className: "slot-amount-label"),
                    Bind = slotRootInstance.Q<Label>(className: "slot-bind-label"),
                    Duration = slotRootInstance.Q<Label>(className: "slot-duration-label")
                };

                // Запекаем индекс слота прямо в userData корневой ноды, чтобы инпуты знали, какой это слот!
                slotCache.Root.userData = i;

                // ВЕБ-ПОДПИСКА НА ИНПУТЫ (Interactions.setup): вешаем события на корень ячейки
                slotCache.Root.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slotCache.Root.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);

                _slots.Add(slotCache);
                Add(slotRootInstance); // Пушим элемент в общее дерево сетки
            }

            RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.LeftShift) IsShiftPressed = true; });
            RegisterCallback<KeyUpEvent>(evt => { if (evt.keyCode == KeyCode.LeftShift) IsShiftPressed = false; });
        }

        public void RequestRefresh() { _isDirty = true; Refresh(); }
        public void SetDataSource(List<string> dataSource) { _dataSource = dataSource; RequestRefresh(); }

        // Универсальный рефреш всей сетки (Твой M:refresh)
        public void Refresh()
        {
            _isDirty = false;
            bool hidesEmptySlots = (_gridType == "aura_frame");

            for (int i = 0; i < _slots.Count; i++)
            {
                GridSlotNodeCache slot = _slots[i];
                string itemId = (_dataSource != null && i < _dataSource.Count) ? _dataSource[i] : null;

                // Скрытие пустых ячеек для баффов/аур (.display = None)
                bool isSlotActive = !hidesEmptySlots || !string.IsNullOrEmpty(itemId);
                slot.Root.style.display = isSlotActive ? DisplayStyle.Flex : DisplayStyle.None;

                if (!string.IsNullOrEmpty(itemId))
                {
                    var itemCfg = ItemsDatabase.GetItem(itemId);
                    // Слепо швыряем структуру в Layout. Он занимается ТОЛЬКО внутренними нодами!
                    StaticGridLayout.DrawSlot(slot, itemCfg, _gridType, i);
                }
                else
                {
                    StaticGridLayout.ClearSlotVisual(slot);
                }
            }
        }

        public string GetSlot(int index)
        {
            if (_dataSource == null || index < 0 || index >= _dataSource.Count) return null;
            return _dataSource[index];
        }

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            var slotRoot = evt.currentTarget as VisualElement;
            if (slotRoot == null || slotRoot.userData == null) return;
            int slotIndex = (int)slotRoot.userData;

            if (evt.button == 1) // ПКМ (mouse_right)
            {
                Debug.Log($"🎯 ПКМ по ячейке: Индекс {slotIndex} | В режиме: {_gridType}");
                evt.StopPropagation();
                return;
            }

            if (evt.button == 0) // ЛКМ
            {
                string itemId = GetSlot(slotIndex);
                if (!string.IsNullOrEmpty(itemId))
                {
                    Debug.Log($"鼠标 ЛКМ по шмотке '{itemId}' в слоте {slotIndex}");
                    // TODO: DragManager.StartDrag!
                }
            }
        }

        private void OnSlotPointerUp(PointerUpEvent evt)
        {
            var slotRoot = evt.currentTarget as VisualElement;
            if (slotRoot == null || slotRoot.userData == null) return;
            int slotIndex = (int)slotRoot.userData;

            // СЦЕНАРИЙ Б: УСПЕШНЫЙ СБРОС ДРАГА (Твой M:on_drop)
            Debug.Log($"🎯 ПРЕДМЕТ СБРОШЕН! Ячейка-цель: Индекс {slotIndex}");
            // TODO: DragManager.FinishDrag(this, slotIndex);
            evt.StopPropagation();
        }

        // Запустить сочную WoW-анимацию шторок ГКД (Твой trigger_gcd)
        public void TriggerGcd(float durationSeconds)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                string itemId = GetSlot(i);
                if (string.IsNullOrEmpty(itemId)) continue;

                var itemCfg = ItemsDatabase.GetItem(itemId);
                if (itemCfg != null && itemCfg.properties.triggers_gcd)
                {
                    // TODO: Накатить conic-gradient на slot.GcdOverlay через нативный таймер
                }
            }
        }

        public void SetSlotColors(int index, Color iconColor, Color bindColor)
        {
            if (index < 0 || index >= _slots.Count) return;
            StaticGridLayout.SetSlotColors(_slots[index], iconColor, bindColor);
        }
    }
}


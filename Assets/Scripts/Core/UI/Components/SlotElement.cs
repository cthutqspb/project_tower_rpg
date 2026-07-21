using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    // 👑 УНИВЕРСАЛЬНЫЙ СЛОТ-ВИДЖЕТ (PascalCase, без подчеркиваний!)
    public class SlotElement : VisualElement
    {
        private VisualElement _iconNode;
        private VisualElement _cooldownOverlayNode;
        private Label _bindLabelNode;
        private Label _amountLabelNode;
        private Label _durationLabelNode;

        public int SlotIndex { get; private set; }

        public SlotElement(VisualTreeAsset template, int index)
        {
            SlotIndex = index;
            template.CloneTree(this); 

            // Кэшируем ноды по их CSS-классам из SlotElement.uss
            _iconNode = this.Q<VisualElement>(className: "slot-icon");
            _cooldownOverlayNode = this.Q<VisualElement>(className: "slot-cooldown-overlay");
            _bindLabelNode = this.Q<Label>(className: "slot-bind-label");
            _amountLabelNode = this.Q<Label>(className: "slot-amount-label");
            _durationLabelNode = this.Q<Label>(className: "slot-duration-label");

            ClearVisual();
        }

        public void DrawSlot(ItemConfig itemCfg, string gridType)
        {
            _bindLabelNode.text = "";
            _amountLabelNode.text = "";
            _durationLabelNode.text = "";
            RemoveFromClassList("disabled");
            RemoveFromClassList("insufficient-mana");

            if (itemCfg == null)
            {
                ClearVisual();
                return;
            }

            _iconNode.style.display = DisplayStyle.Flex;
            
            // Затычка цветом качества из твоей JSON базы
            if (itemCfg.identity.quality == "rare") _iconNode.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
            else if (itemCfg.identity.quality == "uncommon") _iconNode.style.backgroundColor = new Color(0.2f, 0.7f, 0.3f, 1f);
            else _iconNode.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

            // =========================================================================
            // 🧱 TODO: ЗАДЕЛ ПОД ДОМЕНЫ ИНТЕРФЕЙСА (Твой оригинальный код):
            // =========================================================================
            if (gridType == "action_bar") { /* TODO */ }
            else if (gridType == "aura_frame") { /* TODO */ }
        }

        public void ClearVisual()
        {
            _iconNode.style.display = DisplayStyle.None;
            _bindLabelNode.text = "";
            _amountLabelNode.text = "";
            _durationLabelNode.text = "";
            _cooldownOverlayNode.style.backgroundColor = new Color(0, 0, 0, 0);
        }

        public void UpdateCooldownVisual(float progress01)
        {
            if (progress01 <= 0f)
            {
                _cooldownOverlayNode.style.backgroundColor = new Color(0, 0, 0, 0);
                return;
            }
            _cooldownOverlayNode.style.backgroundColor = StyleKeyword.Null;
            // TODO: Накатить conic-gradient
        }
    }
}


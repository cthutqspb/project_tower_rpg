using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    // 🧱 ЧИСТЫЙ КЭШ НОД ЯЧЕЙКИ (Твой GridSlotNodeCache из Defold!)
    public class GridSlotNodeCache
    {
        public VisualElement Root;
        public VisualElement Icon;
        public VisualElement GcdOverlay;
        public Label Amount;
        public Label Bind;
        public Label Duration;
    }

    // 🎨 СЛEПOЙ МOДYЛЬ OТРIСOВКI (Твой StaticGridLayoutModule на C#)
    public static class StaticGridLayout
    {
        // Атомарный сброс и скрытие содержимого ячейки (Твой M.clear_slot_visual)
        public static void ClearSlotVisual(GridSlotNodeCache slot)
        {
            if (slot == null) return;

            slot.Icon.style.display = DisplayStyle.None;
            slot.Amount.text = "";
            if (slot.Bind != null) slot.Bind.text = "";
            if (slot.Duration != null) slot.Duration.text = "";
            
            // Атомарная зачистка ГКД: сбрасываем оверлей в прозрачность
            slot.GcdOverlay.style.backgroundColor = new Color(0, 0, 0, 0);
            slot.Root.RemoveFromClassList("disabled");
            slot.Root.RemoveFromClassList("insufficient-mana");
        }

        // Главный метод отрисовки шмотки в слоте (Твой M.draw_slot один в один!)
        public static void DrawSlot(GridSlotNodeCache slot, ItemConfig itemCfg, string gridType, int index)
        {
            if (slot == null) return;

            // ТИТАНОВОЕ СТEРИЛЬНOЕ OБНYЛEНИЕ HUD (Твой канон): на взлёте тушим оверлеи
            if (slot.Bind != null) slot.Bind.style.display = DisplayStyle.None;
            slot.Amount.style.display = DisplayStyle.None;
            if (slot.Duration != null) slot.Duration.style.display = DisplayStyle.None;
            slot.Root.RemoveFromClassList("disabled");
            slot.Root.RemoveFromClassList("insufficient-mana");

            if (itemCfg == null)
            {
                ClearSlotVisual(slot);
                return;
            }

            // Включаем базовую иконку
            slot.Icon.style.display = DisplayStyle.Flex;

            // Затычка цветом качества из твоей JSON базы данных
            if (itemCfg.identity.quality == "rare") slot.Icon.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
            else if (itemCfg.identity.quality == "uncommon") slot.Icon.style.backgroundColor = new Color(0.2f, 0.7f, 0.3f, 1f);
            else slot.Icon.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

            // =========================================================================
            // ⚔️ ВEТКA А: ЭКШEН-БAР (ПАНЕЛЬ СПОСОБНОСТЕЙ)
            // =========================================================================
            if (gridType == "action_bar")
            {
                if (slot.Bind != null)
                {
                    slot.Bind.style.display = DisplayStyle.Flex;
                    // Авто-маппинг хоткеев: 10->0, 11->"-", 12->"="
                    string bindString = (index == 9) ? "0" : (index == 10) ? "-" : (index == 11) ? "=" : (index + 1).ToString();
                    slot.Bind.text = bindString;
                }

                // TODO: Если у предмета нет use_effects — подкинуть класс затененности:
                // slot.Root.AddToClassList("disabled");
                return;
            }

            // =========================================================================
            // 🦠 ВEТКA В: АYРA-ФРEЙМ (БАФФЫ И ДЕБАФФЫ)
            // =========================================================================
            if (gridType == "aura_frame")
            {
                // Намертво тушим ГКД Pie для аур
                slot.GcdOverlay.style.backgroundColor = new Color(0, 0, 0, 0);

                // TODO: Считать время из data.duration и форматировать %dm/%ds в slot.Duration
                // TODO: Если заряды charges > 1 — выводить стаки в slot.Amount
                return;
            }

            // =========================================================================
            // 🎒 ВEТКA Б: ИНВEНТAРЬ СУМОК
            // =========================================================================
            // TODO: Если предмет stackable и количество > 1 — включить и заполнить slot.Amount
        }

        // Принудительное изменение цветов на уровне видеокарты (Твой M.set_slot_colors)
        public static void SetSlotColors(GridSlotNodeCache slot, Color iconColor, Color bindColor)
        {
            if (slot == null) return;
            slot.Icon.style.backgroundColor = iconColor;
            if (slot.Bind != null) slot.Bind.style.color = bindColor;
        }
    }
}


using System;
using UnityEngine;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI
{
    public static class UIEvents
    {
        public static event System.Action ToggleCharacterWindow;
        public static event System.Action CloseAllWindows;

        // ДОБАВЛЯЕМ СЛЕПЫЕ КАНАЛЫ СВЯЗИ ДЛЯ СЛОТОВ (Твои msg.post аналоги)
        public static event Action<SlotElement, int, string, string, int> OnSlotDoubleClick;
        public static event Action<SlotElement, int, string, string, int, Vector2> OnSlotRightClick;

        public static void TriggerToggleCharacterWindow()
        {
            ToggleCharacterWindow?.Invoke();
        }

        public static void TriggerCloseAllWindows()
        {
            CloseAllWindows?.Invoke();
        }

        // Триггеры для вызова из внутренностей SlotElement
        public static void TriggerSlotDoubleClick(SlotElement slot, int index, string gridType, string itemId, int amount)
        {
            OnSlotDoubleClick?.Invoke(slot, index, gridType, itemId, amount);
        }

        public static void TriggerSlotRightClick(SlotElement slot, int index, string gridType, string itemId, int amount, Vector2 mousePos)
        {
            OnSlotRightClick?.Invoke(slot, index, gridType, itemId, amount, mousePos);
        }
    }
}


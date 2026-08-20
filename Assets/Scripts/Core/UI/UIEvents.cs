using System;
using UnityEngine;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities; // Нагло дописываем юзинг для Entity, чтобы мост знал номера сущностей!

namespace ProjectTowerRpg.Core.UI
{
    public static class UIEvents
    {
        public static event System.Action ToggleCharacterWindow;
        
        public static event Action<Entity> OpenContainerWindow;
        
        public static event System.Action CloseAllWindows;

        // ДОБАВЛЯЕМ СЛЕПЫЕ КАНАЛЫ СВЯЗИ ДЛЯ СЛОТОВ
        public static event Action<SlotElement, int, string, int> OnSlotDoubleClick;
        public static event Action<SlotElement, int, string, int, Vector2> OnSlotRightClick;

        public static void TriggerToggleCharacterWindow()
        {
            ToggleCharacterWindow?.Invoke();
        }

        public static void TriggerOpenContainerWindow(Entity containerEntity)
        {
            OpenContainerWindow?.Invoke(containerEntity);
        }

        public static void TriggerCloseAllWindows()
        {
            CloseAllWindows?.Invoke();
        }

        // Триггеры для вызова из внутренностей SlotElement
        public static void TriggerSlotDoubleClick(SlotElement slot, int index, string itemId, int amount)
        {
            OnSlotDoubleClick?.Invoke(slot, index, itemId, amount);
        }

        public static void TriggerSlotRightClick(SlotElement slot, int index, string itemId, int amount, Vector2 mousePos)
        {
            OnSlotRightClick?.Invoke(slot, index, itemId, amount, mousePos);
        }    
    }
}


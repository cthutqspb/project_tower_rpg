using System;
using UnityEngine;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities; // Нагло дописываем юзинг для Entity, чтобы мост знал номера сущностей!

namespace ProjectTowerRpg.Core.UI
{
    public struct UiClickContext
    {
        public Entity ContextEntity;  // Сущность контейнера/объекта
        public int SlotIndex;         // Индекс ячейки (-1 для клика по фрейму целиком)
        public string TargetId;       // ItemId / SpellId / AuraId
        public int Amount;            // Количество предметов в стаке
        public Vector2 MousePosition; // Позиция клика для контекстного меню
        public int MouseButton;       // ✅ 0 = ЛКМ, 1 = ПКМ
        public int ClickCount;        // ✅ 1 = Одиночный клик, 2 = Даблклик
    }

    public static class UIEvents
    {
        public static event System.Action ToggleCharacterWindow;
        
        public static event Action<Entity> OpenContainerWindow;
        public static event Action<Entity> CloseContainerWindow;

        public static event System.Action CloseAllWindows;
 
        public static event Action<UiClickContext> OnUiClick;
        // ДОБАВЛЯЕМ СЛЕПЫЕ КАНАЛЫ СВЯЗИ ДЛЯ СЛОТОВ
        // public static event Action<SlotElement, int, string, int> OnSlotDoubleClick;
        // public static event Action<SlotElement, int, string, int, Vector2> OnSlotRightClick;

        public static event Action OnActionsMenuClosed;
        
        public static void TriggerToggleCharacterWindow()
        {
            ToggleCharacterWindow?.Invoke();
        }

        public static void TriggerOpenContainerWindow(Entity containerEntity)
        {
            OpenContainerWindow?.Invoke(containerEntity);
        }

        public static void TriggerCloseContainerWindow(Entity containerEntity)
        {
            CloseContainerWindow?.Invoke(containerEntity);
        }

        public static void TriggerCloseAllWindows()
        {
            CloseAllWindows?.Invoke();
        }

        public static void TriggerUiClick(UiClickContext context) => OnUiClick?.Invoke(context);

        // Триггеры для вызова из внутренностей SlotElement
        // public static void TriggerSlotDoubleClick(SlotElement slot, int index, string itemId, int amount)
        // {
        //     OnSlotDoubleClick?.Invoke(slot, index, itemId, amount);
        // }
        //
        // public static void TriggerSlotRightClick(SlotElement slot, int index, string itemId, int amount, Vector2 mousePos)
        // {
        //     OnSlotRightClick?.Invoke(slot, index, itemId, amount, mousePos);
        // }    

        public static void TriggerActionsMenuClosed()
        {
            OnActionsMenuClosed?.Invoke();
        }
    }
}


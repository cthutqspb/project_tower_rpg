using System;
using UnityEngine;
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

        public static event Action OnActionsMenuClosed;

        public static event Action<MenuAction, ActionsMenu.MenuActionData> OnMenuActionSelected;

        public static System.Action OnToggleMainMenu;

        public static System.Action<int> OnFlashSlot;
        
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

        public static void TriggerActionsMenuClosed()
        {
            OnActionsMenuClosed?.Invoke();
        }

        public static void TriggerMenuActionSelected(MenuAction action, ActionsMenu.MenuActionData data)
        {
            OnMenuActionSelected?.Invoke(action, data);
        }

        public static void TriggerToggleMainMenu()
        {
            OnToggleMainMenu?.Invoke();
        }

        public static void TriggerSlotFlash(int slotIndex) {
            OnFlashSlot?.Invoke(slotIndex);
        }
    }
}


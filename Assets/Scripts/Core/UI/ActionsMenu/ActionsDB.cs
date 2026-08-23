using System.Collections.Generic;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI
{
    public static class ActionsMenuDB
    {
        // ================================================================
        // ОБЩИЕ ДЕЙСТВИЯ ДЛЯ ВСЕХ ПРЕДМЕТОВ В ИНВЕНТАРЕ
        // ================================================================
        private static readonly List<MenuAction> SharedItemActions = new List<MenuAction>
        {
            new MenuAction { NameKey = "menu_drop", ActionType = "item_drop" },
            new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
        };

        // ================================================================
        // МАТРИЦА ДЕЙСТВИЙ ПО ТИПАМ ПРЕДМЕТОВ (GUI)
        // ================================================================
        private static readonly Dictionary<string, List<MenuAction>> GuiActions = new Dictionary<string, List<MenuAction>>
        {
            ["weapon"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_equip", ActionType = "item_transfer" },
                new MenuAction { NameKey = "menu_repair", ActionType = "item_repair" },
                new MenuAction { NameKey = "menu_sharpen", ActionType = "item_sharpen" },
            },
            ["armor"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_equip", ActionType = "item_transfer" },
                new MenuAction { NameKey = "menu_repair", ActionType = "item_repair" },
            },
            ["scroll"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_use", ActionType = "item_use" },
                new MenuAction { NameKey = "menu_learn", ActionType = "item_learn" },
            },
            ["container"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_open", ActionType = "container_open" },
                new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
            },
            ["potion"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_use", ActionType = "item_use" },
            },
        };

        // ================================================================
        // МАТРИЦА ДЕЙСТВИЙ ПО ТИПАМ ОБЪЕКТОВ В МИРЕ
        // ================================================================
        private static readonly Dictionary<string, List<MenuAction>> WorldActions = new Dictionary<string, List<MenuAction>>
        {
            ["item"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_pickup", ActionType = "item_pickup" },
                new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
            },
            ["container_item"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_open", ActionType = "container_open" },
                new MenuAction { NameKey = "menu_lockpick", ActionType = "container_lockpick" },
                new MenuAction { NameKey = "menu_disarm", ActionType = "container_disarm" },
                new MenuAction { NameKey = "menu_take_container", ActionType = "item_pickup", Data = new Dictionary<string, object> { ["is_take_box_action"] = true } },
            },
        };

        private static readonly List<MenuAction> DefaultActions = new List<MenuAction>
        {
            new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
        };

        // ================================================================
        // ПУБЛИЧНЫЙ API
        // ================================================================

        public static List<MenuAction> GetGuiActions(ItemConfig itemConfig, bool isEquipped, bool canSplit, string sourceUrl)
        {
            var result = new List<MenuAction>();

            if (itemConfig == null)
                return DefaultActions;

            string itemType = itemConfig.identity?.type ?? "default";
            
            if (GuiActions.TryGetValue(itemType, out var baseActions))
            {
                foreach (var action in baseActions)
                {
                    if (isEquipped && action.ActionType == "item_transfer")
                    {
                        result.Add(new MenuAction
                        {
                            NameKey = "menu_unequip",
                            ActionType = "item_transfer",
                            Data = new Dictionary<string, object> { ["from_paperdoll"] = true }
                        });
                    }
                    else
                    {
                        result.Add(action);
                    }
                }
            }

            if (canSplit && !isEquipped)
            {
                result.Add(new MenuAction { NameKey = "menu_split", ActionType = "execute_split" });
            }

            foreach (var action in SharedItemActions)
            {
                if (isEquipped && action.ActionType == "item_drop")
                    continue;

                if (sourceUrl.Contains("container_window") && action.ActionType == "item_drop")
                {
                    result.Add(new MenuAction { NameKey = "menu_take", ActionType = "item_loot" });
                }
                else
                {
                    result.Add(action);
                }
            }

            return result;
        }

        public static List<MenuAction> GetWorldActions(ItemConfig itemConfig)
        {
            if (itemConfig == null)
                return DefaultActions;

            string actionType = itemConfig.action_type ?? "default";

            if (WorldActions.TryGetValue(actionType, out var actions))
                return actions;

            return DefaultActions;
        }
    }
}

using System.Collections.Generic;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.ECS.Actions;

namespace ProjectTowerRpg.Core.UI
{
    public static class ActionsMenuDB
    {
        // ================================================================
        // ОБЩИЕ ДЕЙСТВИЯ ДЛЯ ВСЕХ ПРЕДМЕТОВ В ИНВЕНТАРЕ
        // ================================================================
        private static readonly List<MenuAction> SharedItemActions = new List<MenuAction>
        {
            new MenuAction { NameKey = "menu_drop", Action = ItemActions.Drop },
            //new MenuAction { NameKey = "menu_examine", Action = ItemActions.Examine },
        };

        // ================================================================
        // МАТРИЦА ДЕЙСТВИЙ ПО ТИПАМ ПРЕДМЕТОВ (GUI)
        // ================================================================
        private static readonly Dictionary<string, List<MenuAction>> GuiActions = new Dictionary<string, List<MenuAction>>
        {
            ["weapon"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_equip", Action = ItemActions.Transfer },
                //new MenuAction { NameKey = "menu_repair", ActionType = "item_repair" },
                //new MenuAction { NameKey = "menu_sharpen", ActionType = "item_sharpen" },
            },
            ["armor"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_equip", Action = ItemActions.Transfer },
                //new MenuAction { NameKey = "menu_repair", ActionType = "item_repair" },
            },
            ["scroll"] = new List<MenuAction>
            {
                //new MenuAction { NameKey = "menu_use", ActionType = "item_use" },
                //new MenuAction { NameKey = "menu_learn", ActionType = "item_learn" },
            },
            ["container"] = new List<MenuAction>
            {
                new MenuAction { NameKey = "menu_open", Action = ContainerActions.Open },
                //new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
            },
            ["potion"] = new List<MenuAction>
            {
                //new MenuAction { NameKey = "menu_use", ActionType = "item_use" },
            },
        };

        // ================================================================
        // МАТРИЦА ДЕЙСТВИЙ ПО ТИПАМ ОБЪЕКТОВ В МИРЕ
        // ================================================================
        private static readonly Dictionary<string, List<MenuAction>> WorldActions = new Dictionary<string, List<MenuAction>>
        {
            ["item"] = new List<MenuAction>
            {
                // Меч, броня, зелье на земле -> кнопка "Поднять"
                new MenuAction { NameKey = "menu_pickup", Action = ItemActions.Loot }
            },
            ["container_item"] = new List<MenuAction>
            {
                // Сундук/бочка на сцене -> кнопка "Открыть"
                new MenuAction { NameKey = "menu_open", Action = ContainerActions.Open },
                
                // Поднять сундук целиком (если на нем будет разрешающий тег)
                new MenuAction { NameKey = "menu_take_container", Action = ItemActions.Loot }
            },
        };

        private static readonly List<MenuAction> DefaultActions = new List<MenuAction>
        {
            //new MenuAction { NameKey = "menu_examine", ActionType = "object_examine" },
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
                foreach (var menuItem in baseActions)
                {
                    if (menuItem.Action == ItemActions.Transfer)
                    {
                        if (isEquipped)
                        {
                            // Вещь на кукле -> кнопка "Снять" -> направление В ИНВЕНТАРЬ
                            result.Add(new MenuAction
                            {
                                NameKey = "menu_unequip",
                                Action = ItemActions.Transfer,
                                Data = new Dictionary<string, object> { ["target_type"] = "inventory" }
                            });
                        }
                        else
                        {
                            // Вещь в инвентаре -> кнопка "Надеть" -> направление НА КУКЛУ
                            result.Add(new MenuAction
                            {
                                NameKey = "menu_equip",
                                Action = ItemActions.Transfer,
                                Data = new Dictionary<string, object> { ["target_type"] = "paperdoll" }
                            });
                        }
                    }
                    else
                    {
                        result.Add(menuItem);
                    }
                }
            }

            if (canSplit && !isEquipped)
            {
                //result.Add(new MenuAction { NameKey = "menu_split", ActionType = "execute_split" });
            }

            foreach (var menuItem in SharedItemActions)
            {
                if (isEquipped && menuItem.Action == ItemActions.Drop)
                    continue;

                if (sourceUrl.Contains("container_window") && menuItem.Action == ItemActions.Drop)
                {
                    result.Add(new MenuAction { NameKey = "menu_take", Action = ItemActions.Loot });
                }
                else
                {
                    result.Add(menuItem);
                }
            }

            return result;
        }

        public static List<MenuAction> GetWorldActions(ItemConfig itemConfig)
        {
            if (itemConfig == null)
                return DefaultActions;

            // Если у тебя в JSON сундука забито identity.type = "container",
            // мы зряче мапим его на "container_item" для UI мира.
            // А всё остальное (weapon, armor, potion) — это обычный лут ("item").
            string identityType = itemConfig.identity?.type ?? "default";
            string worldKey = (identityType == "container") ? "container_item" : "item";

            // Выуживаем готовый плоский список из матрицы
            if (WorldActions.TryGetValue(worldKey, out var baseActions))
            {
                return baseActions;
            }

            return DefaultActions;
        }
    }
}

namespace ProjectTowerRpg.ECS.Actions
{
    // =========================================================================
    // 🦾 БАЗОВЫЙ ОПКОД КОМАНДЫ (Unmanaged byte, 1 байт в памяти чанка)
    // Сквозная последовательная нумерация для сверхзвукового switch бэкенда!
    // =========================================================================
    public enum ActionKind : byte
    {
        None = 0,
        
        // 🎒 ПРЕДМЕТЫ
        Loot = 1,
        ItemTransfer = 2,
        ItemDrop = 3,
        ItemUse = 4,

        // 📦 КОНТЕЙНЕРЫ (СУНДУКИ)
        OpenContainer = 5,
        ContainerTakeAll = 6,

        // ⚔️ БОЁВКА
        Attack = 7,
        AuraDisable = 8,

        // 🧙‍♂️ ИГРОК / МИР
        Interact = 9,
        MoveTo = 10,
        ActionBarAssign = 11,

    }

    public static class BaseActions 
    {
        public const ActionKind None = ActionKind.None;
    }

    public static class ItemActions
    {
        public const ActionKind Loot = ActionKind.Loot;
        public const ActionKind Transfer = ActionKind.ItemTransfer;
        public const ActionKind Drop = ActionKind.ItemDrop;
        public const ActionKind Use = ActionKind.ItemUse;
    }

    public static class ContainerActions
    {
        public const ActionKind Open = ActionKind.OpenContainer;
        public const ActionKind TakeAll = ActionKind.ContainerTakeAll;
    }

    public static class CombatActions
    {
        public const ActionKind Attack = ActionKind.Attack;
        public const ActionKind AuraDisable = ActionKind.AuraDisable;
    }

    public static class PlayerActions
    {
        public const ActionKind Interact = ActionKind.Interact;
        public const ActionKind MoveTo = ActionKind.MoveTo;
        public const ActionKind ActionBarAssign = ActionKind.ActionBarAssign;
    }
}


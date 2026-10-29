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
        ItemCreate = 5,
        ItemSplit = 6,

        // 📦 КОНТЕЙНЕРЫ (СУНДУКИ)
        OpenContainer = 10,
        ContainerTakeAll = 11,

        // ⚔️ БОЁВКА
        AuraDisable = 21,

        // 🧙‍♂️ ИГРОК / МИР
        Interact = 30,
        ActionBarAssign = 32,
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
        public const ActionKind Create = ActionKind.ItemCreate;
        public const ActionKind Split = ActionKind.ItemSplit;
    }

    public static class ContainerActions
    {
        public const ActionKind Open = ActionKind.OpenContainer;
        public const ActionKind TakeAll = ActionKind.ContainerTakeAll;
    }

    public static class CombatActions
    {
        public const ActionKind AuraDisable = ActionKind.AuraDisable;
    }

    public static class PlayerActions
    {
        public const ActionKind Interact = ActionKind.Interact;
        public const ActionKind ActionBarAssign = ActionKind.ActionBarAssign;     
    }
}


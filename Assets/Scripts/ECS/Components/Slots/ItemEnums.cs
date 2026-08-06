namespace ProjectTowerRpg.ECS.Components
{
    public enum ContainerType : byte
    {
        NONE = 0,
        INVENTORY = 1,
        PAPERDOLL = 2,
        ACTION_BAR = 3,
        AURA_FRAME = 4
    }

    public enum EquipSlot : byte
    {
        NONE = 0,
        HEAD = 1,
        CHEST = 2,
        LEGS = 3,
        MAIN_HAND = 4, // В точности как в JSON!
        OFF_HAND = 5,
        BOOTS = 6,
        GLOVES = 7,
        RING_1 = 8,
        RING_2 = 9,
        NECK = 10,
        RANGED = 11
    }
}


namespace ProjectTowerRpg.Core.Units
{
    // 🎯 Фракции — фиксированный enum. Порядок важен: он индексирует матрицу.
    // Новые фракции добавляй В КОНЕЦ, перед Count.
    public enum FactionId : byte
    {
        NeutralHumanoid = 0,
        Undead = 1,
        NeutralUndead = 2,
        BeastNeutral = 3,
        BeastAggressive = 4,
        GreenDragon = 5,

        Count = 6,
    }
}

using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🛡️ ВТОРИЧНЫЕ БОЕВЫЕ ХАРАКТЕРИСТИКИ ЮНИТА:
    // Чистокровный unmanaged runtime-черновик для формул урона и критов!
    // Сюда пишет только UnitStatsSystem, а читают боевые системы и UI.
    public struct UnitCombatStatsComponent : IComponentData
    {
        public float CritChance;      // Критический удар (%)
        public float HitChance;       // Шанс попадания (%)
        public float AttackSpeed;     // Скорость атаки (секунды между ударами)
        public float DodgeChance;     // Шанс уклонения (%)
        public float ParryChance;     // Шанс парирования (%)
        public float Armor;           // Броня (плоское значение поглощения физ. урона)
        public float MagicResist;     // Сопротивление магии
    }
}


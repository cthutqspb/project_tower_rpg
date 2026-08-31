using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🔥 НАШ ТОЧНЫЙ unmanaged ЭНУМ РАНГОВ (Вместо строкового хардкода "rare", "boss"!)
    public enum UnitRank : byte
    {
        Normal = 0,
        Rare = 1,
        Elite = 2,
        Boss = 3
    }

    // =========================================================================
    // 🧬 ДОМЕН ПРОГРЕССИИ ЮНИТА (Счётчики прокачки и ДНК скейлинга в ОЗУ)
    // =========================================================================
    public struct UnitProgressionComponent : IComponentData
    {
        // 🦾 Динамические runtime-счётчики (Тикают в симуляции, сохраняются SaveManager'ом):
        public int Level;                       // Текущий уровень существа (1, 2, 5...)
        public int ExperienceCurrent;          // Сколько опыта накоплено сейчас (0..)
        public int ExperienceRequired;         // Сколько опыта нужно до лвл-апа
        public int ExperienceReward;           // Сколько опыта получит убийца этой Entity

        // 🦾 Статическая ДНК скейлинга (Запекается из JSON ОДИН раз при спавне):
        public UnitRank Rank;                  // Ранг существа (Normal, Rare, Elite, Boss)
        public float GrowthHealth;             //dbCfg.progression.health_growth (например, 1.12f)
        public float GrowthDamage;             //dbCfg.progression.damage_growth
    }
}


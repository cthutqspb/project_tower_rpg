using System.Collections.Generic;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.Loot
{
    /// <summary>
    /// 🔥 ЕДИНСТВЕННОЕ МЕСТО ГЕНЕРАЦИИ ЛУТА
    /// В синглплеере — локальная генерация.
    /// В MMO — заменить на сетевой запрос к серверу.
    /// </summary>
    public static class LootService
    {
        private static ILootGenerator _generator = new LocalLootGenerator();

        public static List<RolledItem> GenerateLoot(string lootTableId)
        {
            if (string.IsNullOrEmpty(lootTableId))
                return new List<RolledItem>();

            return _generator.GenerateLoot(lootTableId);
        }

        // Для MMO: вызвать один раз при старте
        public static void SetGenerator(ILootGenerator generator)
        {
            _generator = generator;
        }
    }

    public interface ILootGenerator
    {
        List<RolledItem> GenerateLoot(string lootTableId);
    }

    public class LocalLootGenerator : ILootGenerator
    {
        public List<RolledItem> GenerateLoot(string lootTableId)
        {
            return LootTables.GetLoot(lootTableId);
        }
    }
}

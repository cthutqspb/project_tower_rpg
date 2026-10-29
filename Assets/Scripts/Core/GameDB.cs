using UnityEngine;
using System.Text;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;
using ProjectTowerRpg.Core.Dialogue;
using ProjectTowerRpg.Core.Quest;

namespace ProjectTowerRpg.Core.Data
{
    public static class GameDB
    {        
        public static void Initialize()
        {
            Debug.Log("⚙️ [GameDB]: Загрузка баз...");

            ItemsDatabase.Load();
            LootTables.Load();               // 🎯 добавить
            AbilitiesDatabase.Load();        // 🎯 раскомментировать
            AurasDatabase.Load();            // 🎯 добавить
            FactionsDatabase.Load();         // 🎯 добавить
            DialoguesDatabase.Load();        // 🎯 добавить
            QuestsDatabase.Load();           // 🎯 добавить
            UnitsDatabase.Load();            // 🎯 уже есть

            Debug.Log("🌍 [GameDB]: Все базы загружены.");
        }

        /// <summary>
        /// 🎯 Собирает ВСЕ уникальные icon_char из всех баз (для прогрева TMP-атласа).
        /// Возвращает строку без дубликатов.
        /// </summary>
        public static string GetAllIconChars()
        {
            var sb = new StringBuilder();

            // Ауры
            foreach (var cfg in AurasDatabase.GetAll())
                if (cfg?.visuals != null && !string.IsNullOrEmpty(cfg.visuals.icon_char))
                    sb.Append(cfg.visuals.icon_char);

            // Способности
            foreach (var cfg in AbilitiesDatabase.GetAll())
                if (cfg?.visuals != null && !string.IsNullOrEmpty(cfg.visuals.icon_char))
                    sb.Append(cfg.visuals.icon_char);

            // Предметы (если есть icon_char)
            foreach (var cfg in ItemsDatabase.GetAll())
                if (cfg?.visuals != null && !string.IsNullOrEmpty(cfg.visuals.icon_char))
                    sb.Append(cfg.visuals.icon_char);

            // Юниты (если есть icon_char)
            // foreach (var cfg in UnitsDatabase.GetAll())
            //     if (!string.IsNullOrEmpty(cfg.icon_char))
            //         sb.Append(cfg.icon_char);

            // 🎯 Убираем дубликаты (TMP всё равно их игнорирует, но строка короче)
            return DeduplicateChars(sb.ToString());
        }

        private static string DeduplicateChars(string input)
        {
            var seen = new System.Collections.Generic.HashSet<char>();
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
                if (seen.Add(c))
                    sb.Append(c);
            return sb.ToString();
        }
    }
}


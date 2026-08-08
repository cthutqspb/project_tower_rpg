using UnityEngine;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Units;

namespace ProjectTowerRpg.Core.Data
{
    public static class GameDB
    {
        // 1. Выводим базы наружу как строго типизированные свойства-линки
        // Никакой каши типов! C# сразу знает, где шмотки, а где монстры.
        public static class Items
        {
            public static ItemConfig Get(string id) => ItemsDatabase.GetItem(id);
            public static ItemConfig Get(int hashId) => ItemsDatabase.GetItem(hashId);
        }

        public static class Units
        {
            public static UnitConfig Get(string id) => UnitsDatabase.GetUnit(id);
            public static UnitConfig Get(int hashId) => UnitsDatabase.GetUnit(hashId);
        }

        // // TODO: WoW-Канон баз данных на будущее
        // public static class Abilities { ... }
        // public static class Auras { ... }

        /// <summary>
        /// Центральный рубильник: загружает ВООБЩЕ ВСЕ базы данных игры в ОЗУ за один вызов
        /// </summary>
        public static void Initialize()
        {
            Debug.Log("⚙️ [GameDB]: Запущена ультимативная загрузка вселенной баз данных...");

            // Последовательно поднимаем JSON-файлы из StreamingAssets
            ItemsDatabase.Load();
            UnitsDatabase.Load();

            // // TODO: Поднять abilities.json, auras.json, когда они появятся
            // AbilitiesDatabase.Load();

            Debug.Log("🌍 [GameDB]: Все статические конфигурации успешно закешированы в ОЗУ!");
        }
    }
}


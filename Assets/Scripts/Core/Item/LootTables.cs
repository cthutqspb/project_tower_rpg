using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace ProjectTowerRpg.Core.Items
{
    [Serializable]
    public class JsonLootEntry
    {
        public string item_id;
        public float chance;
        public int min_amount;
        public int max_amount;
    }

    // Вспомогательный класс-обертка, если парсишь через стандартный JsonUtility Unity
    // (Если у тебя Newtonsoft.Json, словарь распарсится напрямую, но сделаем универсально)
    [Serializable]
    public class JsonLootTableContainer
    {
        // Сюда можно обернуть списки, но для Neovim-стиля проще использовать 
        // Newtonsoft.Json, так как он шёлково жрёт словари Dictionary<string, List<JsonLootEntry>> из коробки!
    }

    public struct RolledItem
    {
        public string ItemId;
        public int Amount;
    }

    public static class LootTables
    {
        public static bool IsLoaded { get; private set; } = false;
        
        // Живой рантайм-справочник таблиц лута в ОЗУ
        private static Dictionary<string, List<JsonLootEntry>> _tables = new Dictionary<string, List<JsonLootEntry>>();

        /// <summary>
        /// 📂 МЕТОД ЗАГРУЗКИ: Вызывается один раз при старте в GameInitializer.Awake()
        /// </summary>
        public static void Load()
        {
            if (IsLoaded) return;

            // Находим путь к файлу (подставь свою рабочую папку, например Resources или StreamingAssets)
            string filePath = Path.Combine(Application.streamingAssetsPath, "LootTables.json");
            
            // Если у тебя базы лежат в Resources:
            // TextAsset targetJson = Resources.Load<TextAsset>("Data/LootTables");
            
            if (File.Exists(filePath))
            {
                string jsonText = File.ReadAllText(filePath);
                
                // Используем каноничный Newtonsoft.Json (он 100% у тебя есть, раз базы шмоток парсятся со словарями)
                _tables = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<JsonLootEntry>>>(jsonText);
                
                IsLoaded = true;
                Debug.Log($"✅ [LootTables]: База данных таблиц лута шёлково загружена! Успешно запечено таблиц: {_tables.Count}");
            }
            else
            {
                Debug.LogError($"🚨 [LootTables]: Не удалось найти файл таблиц лута по пути: {filePath}");
            }
        }

        /// <summary>
        /// 🎲 РОЛЛ ЛУТА ПРИ ОТКРЫТИИ (Копейка в копейку твоя Lua функция!)
        /// </summary>
        public static List<RolledItem> GetLoot(string tableId)
        {
            var rolledItems = new List<RolledItem>();

            // Если база не загружена или таблицы нет — возвращаем пустой контейнер лута без падения игры
            if (!IsLoaded || string.IsNullOrEmpty(tableId) || !_tables.TryGetValue(tableId, out var table))
            {
                Debug.LogWarning($"⚠️ [LootTables]: Запрошена пустая или несуществующая таблица лута: '{tableId}'");
                return rolledItems;
            }

            foreach (var entry in table)
            {
                // Проверяем математическую вероятность из JSON-строки
                if (UnityEngine.Random.value <= entry.chance)
                {
                    int rolledAmount = UnityEngine.Random.Range(entry.min_amount, entry.max_amount + 1);
                    rolledItems.Add(new RolledItem { ItemId = entry.item_id, Amount = rolledAmount });
                }
            }

            return rolledItems;
        }
    }
}


using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Items
{
    public static class ItemsDatabase
    {
        private static Dictionary<string, ItemConfig> _database = new();
        // Новый кэш для моментального поиска из ECS-систем по числу!
        private static Dictionary<int, ItemConfig> _hashedDatabase = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, "ItemsDatabase.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Items]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, ItemConfig>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    ItemConfig cfg = pair.Value;

                    cfg.id = idStr;

                    if (string.IsNullOrEmpty(cfg.action_type))
                    {
                        cfg.action_type = "item";
                    }

                    // ГЕНЕРИРУЕМ ХЭШ: Берем строку "iron_sword", делаем из нее инт
                    int numericHash = idStr.GetHashCode();
                    _hashedDatabase[numericHash] = cfg;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Items]: Успешно загружен реестр. Шмоток в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Items]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        // Геттер по строке (для UI Toolkit, DragManager и JS-подобной логики)
        public static ItemConfig GetItem(string itemId)
        {
            return _database.TryGetValue(itemId, out var config) ? config : null;
        }

        // КАНОНИЧНЫЙ ГЕТТЕР ДЛЯ ECS (Вызывается из систем и ItemActions)
        public static ItemConfig GetItem(int itemHashId)
        {
            if (_hashedDatabase.TryGetValue(itemHashId, out var config))
            {
                return config;
            }
            Debug.LogWarning($"⚠️ БАЗА ДАННЫХ [Items]: Запрос несуществующего хэша: {itemHashId}");
            return null;
        }
    }
}


using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json; // Подключаем промышленный парсер

namespace ProjectTowerRpg.Core.Items
{
    public static class ItemsDatabase
    {
        // Наша RAM-база данных (Хэш-карта): [строковый_id] = Паспорт вещи
        private static Dictionary<string, ItemConfig> _database = new();
        
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

                // Читаем сырую матрешку из JSON
                _database = JsonConvert.DeserializeObject<Dictionary<string, ItemConfig>>(jsonText);

                // 🎯 ТВОЙ ПРОМЫШЛЕННЫЙ ЗАДЕЛ (Инжекция ID и дефолтных типов прямо в RAM):
                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    ItemConfig cfg = pair.Value;

                    cfg.id = idStr; // Вклеиваем строковый ID в паспорт шмотки

                    // Если экшен-тип забыли указать в JSON, инжектируем дефолтный "item"
                    if (string.IsNullOrEmpty(cfg.action_type))
                    {
                        cfg.action_type = "item";
                    }
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Items]: Успешно загружен реестр. Шмоток в ОЗУ: {_database.Count}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Items]: Ошибка парсинга JSON! Рефактори структуру: {ex.Message}");
            }
        }

        // 🎯 ТВОЙ УНИВЕРСАЛЬНЫЙ ГEТТEР M.get_item(id):
        public static ItemConfig GetItem(string itemId)
        {
            if (_database.TryGetValue(itemId, out var config))
            {
                return config;
            }

            Debug.LogWarning($"⚠️ БАЗА ДАННЫХ [Items]: Запрос несуществующего item_id: '{itemId}'");
            return null;
        }
    }
}


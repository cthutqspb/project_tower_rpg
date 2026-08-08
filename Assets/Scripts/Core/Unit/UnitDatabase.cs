using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Units
{
    public static class UnitsDatabase
    {
        private static Dictionary<string, UnitConfig> _database = new();
        // Нативный кэш для моментального поиска из ECS-систем по числу!
        private static Dictionary<int, UnitConfig> _hashedDatabase = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, "UnitsDatabase.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Units]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, UnitConfig>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    UnitConfig cfg = pair.Value;

                    // Записываем строковый ID внутрь самого конфига
                    cfg.id = idStr;

                    // ГЕНЕРИРУЕМ ХЭШ: Берем строку "skeleton_warrior", делаем из нее инт
                    int numericHash = idStr.GetHashCode();
                    _hashedDatabase[numericHash] = cfg;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Units]: Успешно загружен реестр. Существ в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Units]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        // Геттер по строке (для UI Toolkit, кликов и текстовой логики)
        public static UnitConfig GetUnit(string unitId)
        {
            if (!IsLoaded) Load();
            return _database.TryGetValue(unitId, out var config) ? config : null;
        }

        // КАНОНИЧНЫЙ ГЕТТЕР ДЛЯ ECS (Вызывается из систем патрулирования и ИИ)
        public static UnitConfig GetUnit(int unitHashId)
        {
            if (!IsLoaded) Load();
            if (_hashedDatabase.TryGetValue(unitHashId, out var config))
            {
                return config;
            }
            Debug.LogWarning($"⚠️ БАЗА ДАННЫХ [Units]: Запрос несуществующего хэша монстра: {unitHashId}");
            return null;
        }
    }
}


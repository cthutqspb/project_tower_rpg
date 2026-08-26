using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Abilities
{
    public static class AbilitiesDatabase
    {
        private static Dictionary<string, AbilityConfig> _database = new();
        // Быстрый числовой кэш для моментального поиска способностей из ECS-систем!
        private static Dictionary<int, AbilityConfig> _hashedDatabase = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, "AbilitiesDatabase.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Abilities]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, AbilityConfig>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    AbilityConfig cfg = pair.Value;

                    // Запекаем ID внутрь конфига
                    cfg.id = idStr;

                    // Если тип экшена не указан, ставим фоллбек по умолчанию
                    if (string.IsNullOrEmpty(cfg.action_type))
                    {
                        cfg.action_type = "ability";
                    }

                    // ГЕНЕРИРУЕМ ХЭШ: Берем строку "frostbolt", делаем из нее инт по твоему канону
                    int numericHash = idStr.GetHashCode();
                    _hashedDatabase[numericHash] = cfg;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Abilities]: Успешно загружен реестр способностей. Абилок в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Abilities]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        // Геттер по строке (для UI Toolkit, Drag & Drop и логики интерфейса)
        public static AbilityConfig GetAbility(string abilityId)
        {
            return _database.TryGetValue(abilityId, out var config) ? config : null;
        }

        // КАНОНИЧНЫЙ ГЕТТЕР ДЛЯ ECS (Вызывается из будущих боевых систем и кастбаров)
        public static AbilityConfig GetAbility(int abilityHashId)
        {
            if (_hashedDatabase.TryGetValue(abilityHashId, out var config))
            {
                return config;
            }
            Debug.LogWarning($"⚠️ БАЗА ДАННЫХ [Abilities]: Запрос несуществующего хэша способности: {abilityHashId}");
            return null;
        }
    }
}


using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Auras
{
    public static class AurasDatabase
    {
        private static Dictionary<string, AuraConfig> _database = new();
        private static Dictionary<int, AuraConfig> _hashedDatabase = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, "AurasDatabase.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Auras]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, AuraConfig>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    AuraConfig cfg = pair.Value;

                    cfg.id = idStr;

                    if (string.IsNullOrEmpty(cfg.action_type))
                    {
                        cfg.action_type = "aura";
                    }

                    int numericHash = idStr.GetHashCode();
                    _hashedDatabase[numericHash] = cfg;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Auras]: Успешно загружен реестр аур. Аур в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Auras]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        public static AuraConfig GetAura(string auraId)
        {
            return _database.TryGetValue(auraId, out var config) ? config : null;
        }

        public static AuraConfig GetAura(int auraHashId)
        {
            if (_hashedDatabase.TryGetValue(auraHashId, out var config))
            {
                return config;
            }
            Debug.LogWarning($"⚠️ БАЗА ДАННЫХ [Auras]: Запрос несуществующего хэша ауры: {auraHashId}");
            return null;
        }
    }
}


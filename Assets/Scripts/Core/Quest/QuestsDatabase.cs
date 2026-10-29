using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Quest
{
    public static class QuestsDatabase
 {
        private static Dictionary<string, QuestConfig> _database = new();
        private static Dictionary<int, QuestConfig> _hashedDatabase = new();
        private static Dictionary<string, List<QuestConfig>> _byGiver;

        private static readonly List<QuestConfig> _emptyList = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;
            
            string filePath = Path.Combine(Application.streamingAssetsPath, "QuestsDatabase.json");

            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Quests]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, QuestConfig>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    QuestConfig cfg = pair.Value;

                    cfg.id = idStr;

                    // Запекаем интовый хэш для моментального поиска на бэкенде ECS!
                    int numericHash = idStr.GetHashCode();
                    _hashedDatabase[numericHash] = cfg;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Quests]: Успешно загружен реестр заданий. Квестов в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Quests]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        // Геттер по строке для UI Toolkit (список в Журнале)
        public static QuestConfig GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            return _database.TryGetValue(questId, out var config) ? config : null;
        }

        // Каноничный геттер для ECS-систем по хэшу
        public static QuestConfig GetQuest(int questHashId)
        {
            return _hashedDatabase.TryGetValue(questHashId, out var config) ? config : null;
        }
        
        public static List<QuestConfig> GetQuestsByGiver(string giverNameKey)
        {
            if (string.IsNullOrEmpty(giverNameKey)) return _emptyList;

            if (_byGiver == null)
            {
                _byGiver = new Dictionary<string, List<QuestConfig>>();
                foreach (var pair in _database)
                {
                    string giver = pair.Value.giver_name_key;
                    if (string.IsNullOrEmpty(giver)) continue;

                    if (!_byGiver.TryGetValue(giver, out var list))
                    {
                        list = new List<QuestConfig>();
                        _byGiver[giver] = list;
                    }
                    list.Add(pair.Value);
                }
            }

            // ⚠️ Возвращаем кэшированный список — вызывающий НЕ должен его мутировать
            return _byGiver.TryGetValue(giverNameKey, out var result) ? result : _emptyList;
        }

        public static void Unload()
        {
            _database.Clear();
            _hashedDatabase.Clear();
            _byGiver = null;
            IsLoaded = false;
        }
    }
}


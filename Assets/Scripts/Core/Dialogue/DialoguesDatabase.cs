using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Dialogue
{
    public static class DialoguesDatabase
    {
        private static Dictionary<string, DialogueGraph> _database = new();
        // 🦾 Быстрый числовой кэш для моментального поиска диалогов по хэшу из ECS-систем!
        private static Dictionary<int, DialogueGraph> _hashedDatabase = new();
        
        public static bool IsLoaded { get; private set; } = false;

        public static void Load()
        {
            if (IsLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, "DialoguesDatabase.json");

            if (!File.Exists(filePath))
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Dialogues]: Файл JSON не найден по пути: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _database = JsonConvert.DeserializeObject<Dictionary<string, DialogueGraph>>(jsonText);

                _hashedDatabase.Clear();

                foreach (var pair in _database)
                {
                    string idStr = pair.Key;
                    DialogueGraph graph = pair.Value;

                    // ГЕНЕРИРУЕМ ХЭШ: Берем строку "banshee_zalina", делаем из нее инт по твоему канону
                    int numericHash = new FixedString64Bytes(idStr).GetHashCode();
                    _hashedDatabase[numericHash] = graph;
                }

                IsLoaded = true;
                Debug.Log($"🎮 БАЗА ДАННЫХ [Dialogues]: Успешно загружен реестр диалогов. Деревьев в ОЗУ: {_database.Count} (Хэшировано: {_hashedDatabase.Count})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"🚨 БАЗА ДАННЫХ [Dialogues]: Ошибка парсинга JSON! {ex.Message}");
            }
        }

        // Геттер по строке (для ручных проверок и отладки)
        public static DialogueGraph GetDialogue(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return null;
            return _database.TryGetValue(dialogueId, out var graph) ? graph : null;
        }

        // 🎯 КАНОНИЧНЫЙ ГЕТТЕР ДЛЯ ECS: Вызывается из систем взаимодействия за O(1) без тяжелых строк!
        public static DialogueGraph GetDialogue(int dialogueHashId)
        {
            if (_hashedDatabase.TryGetValue(dialogueHashId, out var graph))
            {
                return graph;
            }
            return null;
        }

        public static void Unload()
        {
            _database.Clear();
            _hashedDatabase.Clear();
            IsLoaded = false;
        }
    }
}


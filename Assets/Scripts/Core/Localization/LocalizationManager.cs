using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ProjectTowerRpg.Core.Localization
{
    public static class LocalizationManager
    {
        // Наш плоский итоговый RAM-словарь (Аналог твоего M.data)
        private static Dictionary<string, string> _localizedStrings = new();
        
        private static string _currentLanguage = "Ru";

        public static bool IsLoaded { get; private set; } = false;

        // Принудительно загрузить язык (Аналог твоего M.load_language)
        public static void LoadLanguage(string lang)
        {
            _currentLanguage = lang;
            _localizedStrings.Clear();

            // Путь к папке конкретного языка: Assets/StreamingAssets/Locales/ru/
            string localeFolderPath = Path.Combine(Application.streamingAssetsPath, "Locales", lang);

            if (!Directory.Exists(localeFolderPath))
            {
                Debug.LogError($"🚨 ЛОКАЛИЗАЦИЯ: Папка языка '{lang}' не найдена по пути: {localeFolderPath}");
                return;
            }

            // Получаем список всех .json файлов в этой папке (ui.json, items.json, etc.)
            string[] jsonFiles = Directory.GetFiles(localeFolderPath, "*.json");

            try
            {
                foreach (string filePath in jsonFiles)
                {
                    string jsonText = File.ReadAllText(filePath);
                    
                    // Парсим файл во временный словарик
                    var categoryDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
                    
                    if (categoryDict == null) continue;

                    // 🎰 ТВОЙ МЕТОД ГЛУБОКОГО СЛИЯНИЯ (Аналог твоего merge_tables):
                    foreach (var pair in categoryDict)
                    {
                        // Заливаем ключи в общую плоскую RAM-карту
                        _localizedStrings[pair.Key] = pair.Value;
                    }
                }

                IsLoaded = true;
                Debug.Log($"🌍 ЛОКАЛИЗАЦИЯ: Успешно загружен язык [{lang}]. Ключей в ОЗУ: {_localizedStrings.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"🚨 ЛОКАЛИЗАЦИЯ: Ошибка глубокого слияния таблиц JSON! Лог: {ex.Message}");
            }
        }

        // 🎯 ТВОЙ УНИВЕРСАЛЬНЫЙ ГEТТEР M.get(key) с дебаг-заглушками:
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // Если ключ найден в плоской RAM-карте — отдаем перевод
            if (_localizedStrings.TryGetValue(key, out var translatedText))
            {
                return translatedText;
            }

            // Твоя каноничная зрячая заглушка, если перевода нет!
            return $"[{key}]";
        }
    }
}


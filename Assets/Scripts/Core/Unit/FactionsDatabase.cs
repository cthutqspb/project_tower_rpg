using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ProjectTowerRpg.Core.Units
{
    public static class FactionsDatabase
    {
        // source -> target -> relation
        private static Dictionary<string, Dictionary<string, FactionRelation>> _relations = new();

        public static bool IsLoaded { get; private set; }

        public static void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Units", "FactionsDatabase.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[FactionsDatabase] Файл не найден: {path}");
                return;
            }

            string json = File.ReadAllText(path);

            // Парсим как Dictionary<string, Dictionary<string, string>> — JSON хранит строки
            var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);

            if (raw == null)
            {
                Debug.LogError("[FactionsDatabase] Не удалось распарсить FactionsDatabase.json");
                return;
            }

            _relations.Clear();

            foreach (var sourcePair in raw)
            {
                var targetMap = new Dictionary<string, FactionRelation>();

                foreach (var targetPair in sourcePair.Value)
                {
                    if (Enum.TryParse<FactionRelation>(targetPair.Value, true, out var relation))
                    {
                        targetMap[targetPair.Key] = relation;
                    }
                    else
                    {
                        Debug.LogWarning($"[FactionsDatabase] Неизвестное отношение '{targetPair.Value}' для {sourcePair.Key} -> {targetPair.Key}");
                    }
                }

                _relations[sourcePair.Key] = targetMap;
            }

            IsLoaded = true;
            Debug.Log($"[FactionsDatabase] Загружено фракций: {_relations.Count}");
        }

        /// <summary>
        /// Получить отношение source -> target.
        /// Если матрица не знает source или target — возвращает Neutral.
        /// </summary>
        public static FactionRelation GetRelation(string sourceFaction, string targetFaction)
        {
            if (string.IsNullOrEmpty(sourceFaction) || string.IsNullOrEmpty(targetFaction))
                return FactionRelation.Neutral;

            if (!_relations.TryGetValue(sourceFaction, out var targetMap))
                return FactionRelation.Neutral;

            if (!targetMap.TryGetValue(targetFaction, out var relation))
                return FactionRelation.Neutral;

            return relation;
        }

        /// <summary>
        /// Удобный хелпер — враждебны ли друг другу.
        /// </summary>
        public static bool IsHostile(string sourceFaction, string targetFaction)
        {
            return GetRelation(sourceFaction, targetFaction) == FactionRelation.Hostile;
        }
    }
}

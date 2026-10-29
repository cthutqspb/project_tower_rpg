using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ProjectTowerRpg.Core.Units
{
    public static class FactionsDatabase
    {
        // 🎯 Плоская матрица [source, target] → relation
        private static FactionRelation[,] _matrix;

        // 🎯 Парсер строки → enum (для загрузки JSON и для спавнера)
        private static readonly Dictionary<string, FactionId> _nameToId = new(StringComparer.OrdinalIgnoreCase)
        {
            { "neutral_humanoid", FactionId.NeutralHumanoid },
            { "undead",           FactionId.Undead },
            { "neutral_undead",   FactionId.NeutralUndead },
            { "beast_neutral",    FactionId.BeastNeutral },
            { "beast_aggressive", FactionId.BeastAggressive },
            { "green_dragon",     FactionId.GreenDragon },
        };

        public static bool IsLoaded { get; private set; }

        public static void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "FactionsDatabase.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[FactionsDatabase] Файл не найден: {path}");
                return;
            }

            string json = File.ReadAllText(path);
            var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);

            if (raw == null)
            {
                Debug.LogError("[FactionsDatabase] Не удалось распарсить FactionsDatabase.json");
                return;
            }

            int count = (int)FactionId.Count;
            _matrix = new FactionRelation[count, count];

            // 🎯 Инициализация: по умолчанию всё Neutral
            for (int i = 0; i < count; i++)
                for (int j = 0; j < count; j++)
                    _matrix[i, j] = FactionRelation.Neutral;

            foreach (var sourcePair in raw)
            {
                if (!_nameToId.TryGetValue(sourcePair.Key, out var sourceId))
                {
                    Debug.LogWarning($"[FactionsDatabase] Неизвестная фракция-источник: '{sourcePair.Key}'");
                    continue;
                }

                foreach (var targetPair in sourcePair.Value)
                {
                    if (!_nameToId.TryGetValue(targetPair.Key, out var targetId))
                    {
                        Debug.LogWarning($"[FactionsDatabase] Неизвестная фракция-цель: '{targetPair.Key}'");
                        continue;
                    }

                    if (Enum.TryParse<FactionRelation>(targetPair.Value, true, out var relation))
                    {
                        _matrix[(int)sourceId, (int)targetId] = relation;
                    }
                    else
                    {
                        Debug.LogWarning($"[FactionsDatabase] Неизвестное отношение '{targetPair.Value}' для {sourcePair.Key} -> {targetPair.Key}");
                    }
                }
            }

            IsLoaded = true;
            Debug.Log($"[FactionsDatabase] Загружена матрица {count}x{count}");
        }

        // 🎯 Парсинг строки в enum — для спавнера и загрузки
        public static bool TryParse(string name, out FactionId id)
        {
            if (string.IsNullOrEmpty(name)) { id = FactionId.NeutralHumanoid; return false; }
            return _nameToId.TryGetValue(name, out id);
        }

        public static FactionId Parse(string name)
        {
            return TryParse(name, out var id) ? id : FactionId.NeutralHumanoid;
        }

        // 🎯 Отношение source → target — O(1) по enum
        public static FactionRelation GetRelation(FactionId source, FactionId target)
        {
            if (_matrix == null) return FactionRelation.Neutral;

            int s = (int)source;
            int t = (int)target;
            if ((uint)s >= (uint)_matrix.GetLength(0) || (uint)t >= (uint)_matrix.GetLength(1))
                return FactionRelation.Neutral;

            return _matrix[s, t];
        }

        public static bool IsHostile(FactionId source, FactionId target)
            => GetRelation(source, target) == FactionRelation.Hostile;

        public static bool IsFriendly(FactionId source, FactionId target)
            => GetRelation(source, target) == FactionRelation.Friendly;

        public static bool IsNeutral(FactionId source, FactionId target)
            => GetRelation(source, target) == FactionRelation.Neutral;
    }
}

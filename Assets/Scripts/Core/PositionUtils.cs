using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.Utils
{
    public static class PositionUtils
    {
        /// <summary>
        /// Получает позицию сущности (полиморфно)
        /// </summary>
        public static float3 GetEntityPosition(Entity entity, EntityManager em)
        {
            // 1. Игрок — через PlayerUtils
            if (em.HasComponent<PlayerTag>(entity))
            {
                if (PlayerUtils.TryGetPosition(out float3 pos))
                    return pos;
                return float3.zero;
            }

            // 2. Сущность с LocalTransform
            if (em.HasComponent<LocalTransform>(entity))
            {
                return em.GetComponentData<LocalTransform>(entity).Position;
            }

            // 3. Fallback
            Debug.LogWarning($"[PositionUtils] Не удалось получить позицию для сущности {entity.Index}");
            return float3.zero;
        }

        /// <summary>
        /// Получает позицию сущности (альтернативный метод, если не нужна проверка)
        /// </summary>
        public static float3 GetPosition(Entity entity, EntityManager em)
        {
            return em.HasComponent<LocalTransform>(entity)
                ? em.GetComponentData<LocalTransform>(entity).Position
                : float3.zero;
        }

        /// <summary>
        /// Вычисляет дистанцию между двумя сущностями
        /// </summary>
        public static float GetDistance(Entity a, Entity b, EntityManager em)
        {
            float3 posA = GetEntityPosition(a, em);
            float3 posB = GetEntityPosition(b, em);
            return math.distance(posA, posB);
        }

        /// <summary>
        /// Проверяет, находится ли сущность в радиусе от другой сущности
        /// </summary>
        public static bool IsInRange(Entity a, Entity b, float range, EntityManager em)
        {
            return GetDistance(a, b, em) <= range;
        }
    }
}

using UnityEngine;
using Unity.Mathematics;

namespace ProjectTowerRpg.Core
{
    public static class PhysicsUtils
    {
        /// <summary>
        /// Прощупывает точную высоту поверхности (Terrain/MeshCollider) в указанной точке мира.
        /// </summary>
        /// <param name="currentPosition">Текущая позиция объекта (игрока, предмета, скелета)</param>
        /// <param name="defaultY">Высота по умолчанию, если луч улетел в бездну за карту</param>
        /// <returns>Точная высота Y поверхности земли</returns>
        public static float GetGroundHeight(float3 currentPosition, float defaultY = 0f)
        {
            // Задираем старт луча на 5 метров вверх. 
            Vector3 rayStart = new Vector3(currentPosition.x, currentPosition.y + 5.0f, currentPosition.z);

            // 🌟 ИСПРАВЛЕНО: Добавлен флаг QueryTriggerInteraction.Ignore!
            // Теперь луч напрочь проигнорирует триггерную капсулу на голове персонажа
            // и шёлково врежется строго под ноги — в хардварный Terrain или MeshCollider лестницы!
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y; // Возвращаем чистую высоту холма
            }

            return defaultY; // Страховка (уровень моря)
        }
    }
}


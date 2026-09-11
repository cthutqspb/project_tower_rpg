using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Camera = UnityEngine.Camera;
using Debug = UnityEngine.Debug;
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

        /// <summary>
        /// 🦾 ММО-КАНОН ВЫЛЕТА ЛУТА: Вычисляет точку сброса предмета перед игроком 
        /// с учетом направления камеры и сочной веерной погрешностью, как в WoW/WC3!
        /// </summary>
        public static float3 GetDropPosition(float3 entityPosition)
        {
            var camera = Camera.main;
            // Гвард: если камеры нет — просто кидаем шмотку чуть впереди-справа от игрока
            if (camera == null) 
            {
                return entityPosition + new float3(1.5f, 0f, 1.5f);
            }

            // Вытаскиваем вектор взгляда камеры и намертво проецируем его на плоскость земли (X-Z)
            var forward = (float3)camera.transform.forward;
            forward.y = 0;
            forward = math.normalize(forward);

            // 🎯 СИ-РАНДОМ БЕЗ АЛЛОКАЦИЙ: Используем системный тик времени в качестве сида,
            // чтобы не дергать тяжелый UnityEngine.Random и не мусорить в Managed Heap
            uint seed = (uint)System.DateTime.Now.Ticks;
            if (seed == 0) seed = 1; // Защита от нулевого сида в Unity.Mathematics
            
            var random = new Random(seed);
            
            // Задаем угол веера вылета (разброс влево-вправо)
            float sideAngle = random.NextFloat(-0.3f, 0.3f);
            
            // Закручиваем вектор направления по часовой/против часовой стрелки
            float3 direction = math.mul(quaternion.RotateY(sideAngle), forward);

            // Возвращаем точку: позиция игрока + сочное случайное расстояние вылета перед ним
            return entityPosition + direction * random.NextFloat(0.27f, 0.72f);
        }
    }
}

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Actions
{
    public static class ActionResolver
    {
        public static ActionCommand Resolve(Entity actor, Entity target, EntityManager em)
        {
            // 1. Если нет цели — возвращаем пустую команду
            if (target == Entity.Null)
                return new ActionCommand { Type = "none" };

            // 2. Получаем дистанцию до цели
            float distance = GetDistance(actor, target, em);

            // 3. Проверяем, является ли цель юнитом
            if (em.HasComponent<UnitComponent>(target))
            {
                // 🧟 МОНСТР (можно атаковать)
                if (em.HasComponent<MonsterTag>(target))
                {
                    if (distance <= 1.5f) // TODO: брать дистанцию атаки из оружия
                        return new ActionCommand { Type = "attack", TargetEntity = target };
                    else
                        return new ActionCommand { Type = "move_to", TargetEntity = target, Position = GetPosition(target, em) };
                }

                // 🧑‍💼 NPC (интеракт)
                if (em.HasComponent<NpcTag>(target))
                {
                    return new ActionCommand { Type = "interact", TargetEntity = target };
                }

                // 👤 ИГРОК (PvP)
                if (em.HasComponent<PlayerTag>(target))
                {
                    // TODO: проверка на PvP флаг
                    return new ActionCommand { Type = "attack", TargetEntity = target };
                }
            }

            // 4. Проверяем, является ли цель предметом (Item)
            if (em.HasComponent<ItemComponent>(target))
            {
                var itemComponent = em.GetComponentData<ItemComponent>(target);
                var itemIdStr = itemComponent.ItemId.ToString();
                var itemConfig = ItemsDatabase.GetItem(itemIdStr);

                // 📦 КОНТЕЙНЕР (сундук, труп, матрешка)
                if (itemConfig != null && itemConfig.identity.type == "container")
                {
                    // Можно открыть только в упор
                    if (distance <= 0.72f)
                        return new ActionCommand { Type = "open_container", TargetEntity = target };
                    else
                        return new ActionCommand { Type = "move_to", TargetEntity = target, Position = GetPosition(target, em) };
                }

                // 💎 ОБЫЧНЫЙ ПРЕДМЕТ (лут)
                if (distance <= 0.72f)
                    return new ActionCommand { Type = "loot", TargetEntity = target };
                else
                    return new ActionCommand { Type = "move_to", TargetEntity = target, Position = GetPosition(target, em) };
            }

            // 5. Проверяем, является ли цель объектом (Object)
            if (em.HasComponent<ObjectComponent>(target))
            {
                // TODO: логика для объектов (двери, рычаги, ловушки)
                // Пока просто интеракт
                return new ActionCommand { Type = "interact", TargetEntity = target };
            }

            // 6. Если ничего не подошло
            return new ActionCommand { Type = "none" };
        }

        // ================================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ================================================================

        private static float GetDistance(Entity a, Entity b, EntityManager em)
        {
            if (!em.HasComponent<LocalTransform>(a) || !em.HasComponent<LocalTransform>(b))
                return float.MaxValue;

            var posA = em.GetComponentData<LocalTransform>(a).Position;
            var posB = em.GetComponentData<LocalTransform>(b).Position;
            return math.distance(posA, posB);
        }

        private static float3 GetPosition(Entity entity, EntityManager em)
        {
            return em.HasComponent<LocalTransform>(entity)
                ? em.GetComponentData<LocalTransform>(entity).Position
                : float3.zero;
        }
    }
}

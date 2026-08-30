using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Utils;

namespace ProjectTowerRpg.ECS.Actions
{
    public static class ActionResolver
    {
        public static ActionCommand Resolve(Entity actor, Entity target, EntityManager em)
        {
            // 1. Если нет цели — возвращаем пустую команду
            if (target == Entity.Null)
                return new ActionCommand { Action = ActionKind.None };

            // 2. Получаем дистанцию до цели
            float distance = PositionUtils.GetDistance(actor, target, em);

            // 3. Проверяем, является ли цель юнитом
            if (em.HasComponent<UnitComponent>(target))
            {
                // 🧟 МОНСТР (можно атаковать)
                if (em.HasComponent<MonsterTag>(target))
                {
                    if (distance <= 1.5f) // TODO: брать дистанцию атаки из оружия
                        return new ActionCommand { Action = CombatActions.Attack, TargetEntity = target };
                    else
                        return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
                }

                // 🧑‍💼 NPC (интеракт)
                if (em.HasComponent<NpcTag>(target))
                {
                    return new ActionCommand { Action = PlayerActions.Interact, TargetEntity = target };
                }

                // 👤 ИГРОК (PvP)
                if (em.HasComponent<PlayerTag>(target))
                {
                    // TODO: проверка на PvP флаг
                    return new ActionCommand { Action = CombatActions.Attack, TargetEntity = target };
                }
            }

            // 4. Проверяем, является ли цель предметом (Item)
            if (em.HasComponent<ItemComponent>(target))
            {
                var itemComponent = em.GetComponentData<ItemComponent>(target);
                var itemIdStr = itemComponent.ItemId.ToString();
                var itemConfig = ItemsDatabase.GetItem(itemIdStr);

                Debug.Log($"[ActionResolver] Предмет: {itemIdStr}, Дистанция: {distance:F2}м, Сущность: {target.Index}");

                // 📦 КОНТЕЙНЕР (сундук, труп, матрешка) — проверяем по конфигу из БД
                if (itemConfig != null && itemConfig.identity.type == "container")
                {
                    if (distance <= 0.72f)
                        return new ActionCommand { Action = ContainerActions.Open, TargetEntity = target }; // ← target — сущность!
                    else
                        return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
                }

                // 💎 ОБЫЧНЫЙ ПРЕДМЕТ (лут) — ЛУТАЕМ СУЩНОСТЬ, А НЕ ПОИСК ПО ID!
                if (distance <= 0.72f)
                    return new ActionCommand { Action = ItemActions.Loot, TargetEntity = target }; // ← target — сущность!
                else
                    return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
            }

            // 5. Проверяем, является ли цель объектом (Object)
            if (em.HasComponent<ObjectComponent>(target))
            {
                // TODO: логика для объектов (двери, рычаги, ловушки)
                // Пока просто интеракт
                return new ActionCommand { Action = PlayerActions.Interact, TargetEntity = target };
            }

            // 6. Если ничего не подошло
            return new ActionCommand { Action = ActionKind.None };
        }
    }
}

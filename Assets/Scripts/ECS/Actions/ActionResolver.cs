using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Utils;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Actions
{   
    public static class ActionResolver
    {
        private const float INTERACTION_RANGE = 2.7f;
        private const float LOOT_RANGE = 1.27f;

        /// <summary>
        /// Возвращает кортеж: 
        /// 1. ActionCommand (для UI/Инвентаря/NPC)
        /// 2. InteractRequest? (для 3D-объектов: двери, фонари, рычаги)
        /// </summary>
        public static (ActionCommand command, InteractRequest? interactReq) Resolve(Entity actor, Entity target, EntityManager em)
        {
            // 1. Если нет цели или она стёрта — возвращаем пустую команду
            if (target == Entity.Null || !em.Exists(target))
                return (new ActionCommand { Action = BaseActions.None }, null);

            // 2. Получаем дистанцию до цели
            float distance = PositionUtils.GetDistance(actor, target, em);

            // 3. Проверяем, является ли цель юнитом (Игрок, Моб, Труп)
            if (em.HasComponent<UnitComponent>(target))
            {
                // 🪦 WOW/BG3 СКОЛ СИ-ЛОГИКИ ТРУПOВ:
                if (em.HasComponent<IsDeadTag>(target))
                {
                    if (distance <= LOOT_RANGE)
                        return (new ActionCommand { Action = ContainerActions.Open, TargetEntity = target }, null);
                }

                // ================================================================
                // ДЕВСТВЕННО ЖИВЫЕ ЮНИТЫ (Объединённый зрячий ECS-контур ИИ)
                // ================================================================
                if (em.HasComponent<AiComponent>(target))
                {
                    var aiComponent = em.GetComponentData<AiComponent>(target);

                    // 🦾 1. КЕЙС МИРНОГО ЮНИТА (Баньши Залина, Торговцы, Квестодатели):
                    if (aiComponent.Profile == AiProfileKind.NeutralPassive || aiComponent.Profile == AiProfileKind.Passive)
                    {
                        if (distance <= INTERACTION_RANGE)
                            return (new ActionCommand { Action = PlayerActions.Converse, TargetEntity = target }, null);
                    }
                }
            }

            // 4. Проверяем, является ли цель предметом (Item)
            if (em.HasComponent<ItemComponent>(target))
            {
                var itemComponent = em.GetComponentData<ItemComponent>(target);
                var itemIdStr = itemComponent.ItemId.ToString();
                var itemConfig = ItemsDatabase.GetItem(itemIdStr);

                Debug.Log($"[ActionResolver] Предмет: {itemIdStr}, Дистанция: {distance:F2}м, Сущность: {target.Index}");

                // 📦 КОНТЕЙНЕР (сундук, матрешка)
                if (itemConfig != null && itemConfig.identity.type == "container")
                {
                    if (distance <= LOOT_RANGE)
                        return (new ActionCommand { Action = ContainerActions.Open, TargetEntity = target }, null);
                }

                // 💎 ОБЫЧНЫЙ ПРЕДМЕТ (лут с земли)
                if (distance <= LOOT_RANGE)
                    return (new ActionCommand { Action = ItemActions.Loot, TargetEntity = target }, null);
            }

            // 5. Проверяем, является ли цель интерактивным объектом (Дверь, Фонарь)
            if (em.HasComponent<InteractableComponent>(target))
            {
                // Проверяем дистанцию (используем INTERACTION_RANGE, как для NPC)
                if (distance <= INTERACTION_RANGE)
                {
                    // Возвращаем пустую ActionCommand и заполненный InteractRequest
                    return (new ActionCommand { Action = BaseActions.None }, new InteractRequest 
                    { 
                        Source = actor, 
                        Target = target 
                    });
                }
                else
                {
                    Debug.Log($"[ActionResolver] Объект слишком далеко: {distance:F2}м (Макс: {INTERACTION_RANGE}м)");
                }
            }

            // 6. Если ничего не подошло
            return (new ActionCommand { Action = BaseActions.None }, null);
        }
    }
}

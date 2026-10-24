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


        public static ActionCommand Resolve(Entity actor, Entity target, EntityManager em)
        {
            // 1. Если нет цели или она стёрта — возвращаем пустую команду
            if (target == Entity.Null || !em.Exists(target))
                return new ActionCommand { Action = BaseActions.None };

            // 2. Получаем дистанцию до цели за 0 наносекунд нагрузки
            float distance = PositionUtils.GetDistance(actor, target, em);

            // 3. Проверяем, является ли цель юнитом (Игрок, Моб, Труп)
            if (em.HasComponent<UnitComponent>(target))
            {
                // 🪦 WOW/BG3 СКОЛ СИ-ЛОГИКИ ТРУПOВ:
                // Если на сущности взлетел IsDeadTag — мы нагло перехватываем управление!
                // Нам плевать, кто это был — монстр или игрок, теперь это просто "мешок с лутом".
                if (em.HasComponent<IsDeadTag>(target))
                {
                    // К трупу нужно сначала честно подойти, как к сундуку!
                    if (distance <= LOOT_RANGE)
                        // В упор — шёлково выплёвываем команду открытия контейнера!
                        return new ActionCommand { Action = ContainerActions.Open, TargetEntity = target };
                }

                // ================================================================
                // ДЕВСТВЕННО ЖИВЫЕ ЮНИТЫ (Объединённый зрячий ECS-контур ИИ)
                // ================================================================
                if (em.HasComponent<AiComponent>(target))
                {
                    var aiComponent = em.GetComponentData<AiComponent>(target);

                    // 🦾 1. КЕЙС МИРНОГО ЮНИТА (Баньши Залина, Торговцы, Квестодатели):
                    // Если профиль пассивный или нейтральный — включаем логику разговора/торговли!
                    if (aiComponent.Profile == AiProfileKind.NeutralPassive || aiComponent.Profile == AiProfileKind.Passive)
                    {
                        if (distance <= INTERACTION_RANGE) // WoW-дистанция честного разговора в 3D
                            // В упор — шёлково отдаём приказ на взаимодействие!
                            return new ActionCommand { Action = PlayerActions.Interact, TargetEntity = target };
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

                // 📦 КОНТЕЙНЕР (сундук, матрешка) — проверяем по конфигу из БД
                if (itemConfig != null && itemConfig.identity.type == "container")
                {
                    if (distance <= LOOT_RANGE)
                        return new ActionCommand { Action = ContainerActions.Open, TargetEntity = target };
                }

                // 💎 ОБЫЧНЫЙ ПРЕДМЕТ (лут с земли)
                if (distance <= LOOT_RANGE)
                    return new ActionCommand { Action = ItemActions.Loot, TargetEntity = target };
            }

            // 5. Проверяем, является ли цель объектом (Object)
            if (em.HasComponent<ObjectComponent>(target))
            {
                // TODO: логика для объектов (двери, рычаги, ловушки)
                return new ActionCommand { Action = PlayerActions.Interact, TargetEntity = target };
            }

            // 6. Если ничего не подошло
            return new ActionCommand { Action = BaseActions.None };
        }
    }
}


using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Utils;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Actions
{
    public static class ActionResolver
    {
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
                    if (distance <= 1.27f)
                        // В упор — шёлково выплёвываем команду открытия контейнера!
                        return new ActionCommand { Action = ContainerActions.Open, TargetEntity = target };
                    else
                        // Далеко — отдаём приказ «Беги к координатам тела»!
                        return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
                }

                // ================================================================
                // ДЕВСТВЕННО ЖИВЫЕ ЮНИТЫ (Твой оригинальный контур)
                // ================================================================

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

                // 📦 КОНТЕЙНЕР (сундук, матрешка) — проверяем по конфигу из БД
                if (itemConfig != null && itemConfig.identity.type == "container")
                {
                    if (distance <= 1.27f)
                        return new ActionCommand { Action = ContainerActions.Open, TargetEntity = target };
                    else
                        return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
                }

                // 💎 ОБЫЧНЫЙ ПРЕДМЕТ (лут с земли)
                if (distance <= 1.27f)
                    return new ActionCommand { Action = ItemActions.Loot, TargetEntity = target };
                else
                    return new ActionCommand { Action = PlayerActions.MoveTo, TargetEntity = target, Position = PositionUtils.GetPosition(target, em) };
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


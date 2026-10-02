using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.Abilities
{
    public static class AbilityValidator
    {
        /// <summary>
        /// Главный ААА-шлюз валидации конвейера способностей (Декомпозированный ММО-Канон)
        /// </summary>
        public static CastValidationResult CheckCastPossibility(
            string abilityId, 
            Entity caster, 
            EntityManager em
        )
        {
            // 1. СИ-ГВАРДЫ: Страхуем ОЗУ от пустых строк и потерянных сущностей
            if (string.IsNullOrEmpty(abilityId))
                return new CastValidationResult { IsPossible = false, Reason = "EMPTY_ABILITY_ID" };

            if (caster == Entity.Null || !em.Exists(caster)) 
                return new CastValidationResult { IsPossible = false, Reason = "NO_CASTER" };

            // 2. ВЫУЖИВАЕМ КОНФИГ: Извлекаем чертеж из базы за 0 наносекунд
            var ability = AbilitiesDatabase.GetAbility(abilityId);
            if (ability == null) 
                return new CastValidationResult { IsPossible = false, Reason = "UNKNOWN_ABILITY" };

            // =========================================================================
            // ПОСЛЕДОВАТЕЛЬНЫЙ СИ-КОНВЕЙЕР ПРОВЕРОК
            // =========================================================================

            // ⏳ Слой А: ГКД (Высший приоритет спам-защиты)
            var gcdResult = CheckGCD(ability, caster, em);
            if (!gcdResult.IsPossible) return gcdResult;

            var cooldownResult = CheckCooldown(ability, caster, em);
            if (!cooldownResult.IsPossible) return cooldownResult;

            // 🧪 Слой Б: Стоимость ресурсов (Мана / Энергия / Ярость)
            var resourceResult = CheckResource(ability, caster, em);
            if (!resourceResult.IsPossible) return resourceResult;

            // 🎯 Слой В: Существование и стейт цели (requires_target, IsDead)
            var targetResult = CheckTarget(ability, caster, em, out Entity target);
            if (!targetResult.IsPossible) return targetResult;

            // 📐 Слой Г: Геометрия хитбоксов и Meadows-дистанция (math.distance)
            var distanceResult = CheckDistance(ability, caster, target, em);
            if (!distanceResult.IsPossible) return distanceResult;

            var movementResult = CheckMovement(ability, caster, em);
            if (!movementResult.IsPossible) return movementResult;

            // Способность полностью легальна, конвейер чист!
            return new CastValidationResult { IsPossible = true, Reason = null };
        }

        // =========================================================================
        // ИЗОЛИРОВАННЫЕ ВНУТРЕННИЕ МЕТOДЫ ПРОВЕРОК
        // =========================================================================

        private static CastValidationResult CheckGCD(AbilityConfig ability, Entity caster, EntityManager em)
        {
            // 1. ГВАРД ЧЕРТЕЖА: Если способность из JSON вообще не запускает ГКД — она всегда легальна!
            if (ability.parameters == null || !ability.parameters.triggers_gcd)
            {
                return new CastValidationResult { IsPossible = true, Reason = null };
            }

            // 2. СИ-ЗАЩИТА ОЗУ: Проверяем наличие боевого паспорта на кастере
            if (!em.HasComponent<CombatStateComponent>(caster))
            {
                return new CastValidationResult { IsPossible = true, Reason = null };
            }

            var combatState = em.GetComponentData<CombatStateComponent>(caster);

            // ⏳ КАНOНИЧНЫЙ WOW-ГВАРД ГКД С ЗАЩИТОЙ ИНСТАНТ-СПАМА:
            if (combatState.GcdRemaining > 0f)
            {
                float baseGcdDuration = combatState.GcdDuration;

                // Проверяем, взвелось ли ГКД только что в этом же самом кадре (находится на самом пике).
                // Твоя дельта-страховка в 0.02 секунды защищает инстант-баффы от ложных интерфейсных рефрешей в конце кадра!
                bool isJustTriggered = combatState.GcdRemaining >= (baseGcdDuration - 0.02f);

                // Если ГКД реально тикает и остывает (меньше пика) — это наглый спам кнопки! НАМЕРТВО БЛОКИРУЕМ!
                if (!isJustTriggered)
                {
                    // Вердикт: "Категория не готова!"
                    return new CastValidationResult { IsPossible = false, Reason = "GCD_ACTIVE" };
                }
            }

            // Глобальный кулдаун остыл, Meadows-конвейер чист!
            return new CastValidationResult { IsPossible = true, Reason = null };
        }

        private static CastValidationResult CheckCooldown(AbilityConfig ability, Entity caster, EntityManager em)
        {
            // Если у способности в базе вообще нет КД, проверку скипаем со свистом
            if (ability.parameters == null || ability.parameters.cooldown <= 0f)
                return new CastValidationResult { IsPossible = true, Reason = null };

            // Определяем целевую группу КД. Если в конфиге null — падаем на дефолтный abilityId
            FixedString32Bytes cooldownGroup = string.IsNullOrEmpty(ability.parameters.cooldown_group)
                ? ability.id
                : ability.parameters.cooldown_group;
            
            if (em.HasBuffer<ActiveCooldownElement>(caster))
            {
                var cooldownsBuffer = em.GetBuffer<ActiveCooldownElement>(caster, isReadOnly: true);        
                // Плоский Си-поиск по буферу активных КД
                for (int i = 0; i < cooldownsBuffer.Length; i++)
                {
                    var cooldownElement = cooldownsBuffer[i];
                    
                    // Если нашли нашу группу и таймер еще тикает — наглухо блокируем конвейер!
                    if (cooldownElement.CooldownGroup == cooldownGroup && cooldownElement.Remaining > 0f)
                    {
                        return new CastValidationResult 
                        { 
                            IsPossible = false, 
                            Reason = "COOLDOWN_ACTIVE" 
                        };
                    }
                }
            }

            return new CastValidationResult { IsPossible = true, Reason = null };
        }


        private static CastValidationResult CheckResource(AbilityConfig ability, Entity caster, EntityManager em)
        {
            // 1. ГВАРД ЧЕРТЕЖА: Если у способности в JSON нет секции стоимости — спелл бесплатный!
            if (ability.cost == null || string.IsNullOrEmpty(ability.cost.resource))
            {
                return new CastValidationResult { IsPossible = true, Reason = null };
            }

            // Вытаскиваем строковое имя требуемого ресурса из конфига (например, "Mana" или "Energy")
            string requiredResource = ability.cost.resource;
            float resourceCost = ability.cost.value;

            // Если способность бесплатная (цена 0) — пропускаем без проверок
            if (resourceCost <= 0f)
            {
                return new CastValidationResult { IsPossible = true, Reason = null };
            }

            // 2. СИ-ЗАЩИТА ОЗУ: Если на кастере физически нет компонента ресурсов (например, это пилон или вещь)
            if (!em.HasComponent<ResourceComponent>(caster))
            {
                return new CastValidationResult { IsPossible = false, Reason = "NO_RESOURCE_COMPONENT" };
            }

            // Достаем плоский unmanaged-паспорт ресурсов существа из чанка памяти
            var currentResources = em.GetComponentData<ResourceComponent>(caster);

            // Если у существа вообще нет ресурса (тип None) — каст заблокирован
            if (currentResources.Type == ResourceType.None)
            {
                return new CastValidationResult { IsPossible = false, Reason = "NO_MANA" };
            }

            // 3. АППАРАТНЫЙ СВИТЧ ПРОВЕРКИ (Wow-Канон полиморфизма ресурсов)
            // Мы парсим строку из JSON в твой нативный C# enum ResourceType за 0 наносекунд
            if (System.Enum.TryParse<ResourceType>(requiredResource, true, out var requiredType))
            {
                // Проверяем, совпадает ли биологический тип энергии спелла с тем, что сейчас залито в тушу кастера
                if (currentResources.Type != requiredType)
                {
                    return new CastValidationResult { IsPossible = false, Reason = "INVALID_RESOURCE_TYPE" };
                }

                // Хладнокровно сверяем текущее количество Си-байт ресурса с ценой из базы данных
                if (currentResources.Current < resourceCost)
                {
                    // Если маны/энергии мало — наглухо блокируем! 
                    // Твой SlotElement.SetData поймает этот Reason и сочно затемнит иконку абилки!
                    return new CastValidationResult { IsPossible = false, Reason = "NO_MANA" };
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[AbilityValidator]: Ошибка парсинга типа ресурса '{requiredResource}' в конфиге абилки {ability.id}");
                return new CastValidationResult { IsPossible = false, Reason = "UNKNOWN_RESOURCE_TYPE" };
            }

            // Ресурсов хватает, Meadows-конвейер чист!
            return new CastValidationResult { IsPossible = true, Reason = null };
        }


        private static CastValidationResult CheckTarget(AbilityConfig ability, Entity caster, EntityManager em, out Entity target)
        {
            target = Entity.Null;

            if (ability.parameters != null && ability.parameters.requires_target)
            {
                // Вытаскиваем текущую цель напрямую из боевого стейта кастера
                if (em.HasComponent<CombatStateComponent>(caster))
                {
                    target = em.GetComponentData<CombatStateComponent>(caster).CurrentTarget;
                }

                // КЕЙС А: Способность требует врага, а в таргете кастера пусто -> Отказ!
                if (target == Entity.Null || !em.Exists(target))
                {
                    return new CastValidationResult { IsPossible = false, Reason = "NO_TARGET" };
                }

                // КЕЙС Б: Цель найдена, но она уже мертва -> Отказ!
                if (em.HasComponent<IsDeadTag>(target))
                {
                    return new CastValidationResult { IsPossible = false, Reason = "INVALID_TARGET" };
                }
            }

            return new CastValidationResult { IsPossible = true, Reason = null };
        }

        private static CastValidationResult CheckDistance(AbilityConfig ability, Entity caster, Entity target, EntityManager em)
        {
            if (ability.parameters != null && ability.parameters.requires_target)
            {
                // КЕЙС В: Расчет Edge-to-Edge расстояния в мире через хитбоксы WoW-канона
                if (em.HasComponent<LocalTransform>(caster) && em.HasComponent<LocalTransform>(target))
                {
                    float3 casterPosition = em.GetComponentData<LocalTransform>(caster).Position;
                    float3 targetPosition = em.GetComponentData<LocalTransform>(target).Position;

                    // Вычисляем чистую дистанцию между точками в ОЗУ мира
                    float distance = math.distance(casterPosition, targetPosition);

                    // Извлекаем базовый ренж из Data-Driven параметров конфига
                    float baseRange = ability.parameters.range;
                    float maxAllowedRange = baseRange;

                    // Дифференциация ближнего и дальнего боя по хитбоксам (WoW-канон)
                    if (baseRange < 0.4f)
                    {
                        float casterRadius = em.HasComponent<MovementComponent>(caster) 
                            ? em.GetComponentData<MovementComponent>(caster).HitboxRadius 
                            : 0f;
                            
                        float targetRadius = em.HasComponent<MovementComponent>(target) 
                            ? em.GetComponentData<MovementComponent>(target).HitboxRadius 
                            : 0f;

                        maxAllowedRange = baseRange + casterRadius + targetRadius;
                    }
                    else
                    {
                        float targetRadius = em.HasComponent<MovementComponent>(target) 
                            ? em.GetComponentData<MovementComponent>(target).HitboxRadius 
                            : 0f;

                        maxAllowedRange = baseRange + targetRadius;
                    }

                    // Если цель разорвала Meadows-дистанцию — Отказ!
                    if (distance > maxAllowedRange)
                    {
                        return new CastValidationResult { IsPossible = false, Reason = "OUT_OF_RANGE" };
                    }
                }
            }

            return new CastValidationResult { IsPossible = true, Reason = null };
        }

        private static CastValidationResult CheckMovement(AbilityConfig ability, Entity caster, EntityManager em)
        {
            if (ability == null || ability.parameters == null)
                return new CastValidationResult { IsPossible = true };

            // Если способность мгновенная (инстант) или поддерживаемая на ходу — бег разрешен!
            if (ability.parameters.cast_time <= 0.01f || ability.parameters.is_channeling)
            {
                return new CastValidationResult { IsPossible = true };
            }

            // Если у юнита есть компонент движения — проверяем, бежит ли он прямо сейчас
            if (em.HasComponent<ProjectTowerRpg.ECS.Components.MovementComponent>(caster))
            {
                var movement = em.GetComponentData<ProjectTowerRpg.ECS.Components.MovementComponent>(caster);
                
                // Проверяем вектор направления и скорость (Wow-канон WASD-гварда)
                bool isMoving = Unity.Mathematics.math.lengthsq(movement.Direction) > 0.001f && movement.CurrentSpeed > 0.001f;

                if (isMoving)
                {
                    // Намертво блокируем старт! Кнопка экшен-бара покрасится по этой причине
                    return new CastValidationResult { IsPossible = false, Reason = "MOVING" };
                }
            }

            return new CastValidationResult { IsPossible = true };
        }

    }
}


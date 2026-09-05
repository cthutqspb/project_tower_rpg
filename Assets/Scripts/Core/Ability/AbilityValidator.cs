using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.Abilities
{
    public static class AbilityValidator
    {
        /// <summary>
        /// Универсальный ААА-Валидатор возможности применения способностей (Только Цели и Дистанция)
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

            // 2. ВЫУЖИВАЕМ КОНФИГ: Метод сам достает чертеж из базы за 0 наносекунд нагрузки!
            var ability = AbilitiesDatabase.GetAbility(abilityId);
            if (ability == null) 
                return new CastValidationResult { IsPossible = false, Reason = "UNKNOWN_ABILITY" };

            // 🎯 3. ПОЛИМОРФНЫЙ ГВАРД ТАРГЕТИНГА (requires_target)
            if (ability.parameters != null && ability.parameters.requires_target)
            {
                // Вытаскиваем текущую цель напрямую из боевого стейта кастера!
                Entity target = Entity.Null;
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
                if (em.HasComponent<CombatStateComponent>(target))
                {
                    var combat = em.GetComponentData<CombatStateComponent>(target);
                    if (combat.IsDead) 
                        return new CastValidationResult { IsPossible = false, Reason = "INVALID_TARGET" };
                }

                // КЕЙС В: Расчет Edge-to-Edge расстояния в мире через хитбоксы WoW-канона
                if (em.HasComponent<LocalTransform>(caster) && em.HasComponent<LocalTransform>(target))
                {
                    float3 casterPos = em.GetComponentData<LocalTransform>(caster).Position;
                    float3 targetPos = em.GetComponentData<LocalTransform>(target).Position;

                    // Вычисляем чистую дистанцию между точками в ОЗУ мира
                    float dist = math.distance(casterPos, targetPos);

                    // Извлекаем базовый ренж из Data-Driven параметров конфига
                    float baseRange = ability.parameters.range;
                    float maxAllowedRange = baseRange;

                    // Дифференциация ближнего и дальнего боя по хитбоксам (WoW-канон)
                    if (baseRange < 2.7f)
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
                    if (dist > maxAllowedRange)
                    {
                        return new CastValidationResult { IsPossible = false, Reason = "OUT_OF_RANGE" };
                    }
                }
            }

            // Способность легальна, конвейер чист!
            return new CastValidationResult { IsPossible = true, Reason = null };
        }
    }
}


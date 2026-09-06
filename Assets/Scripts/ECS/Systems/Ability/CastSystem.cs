using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CastSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            // =========================================================================
            // ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Серверный шлюз на старте)
            // =========================================================================
            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CastRequest>>().WithEntityAccess())
            {
                Entity casterEntity = request.ValueRO.Caster;
                string abilityIdStr = request.ValueRO.AbilityId.ToString();

                if (casterEntity != Entity.Null && em.Exists(casterEntity))
                {
                    // 🚀 ШАГ 1: Сначала хладнокровно прогоняем абсолютный шлюз безопасности!
                    // Если ГКД тикает — метод выдаст GCD_ACTIVE, и спам кнопки мгновенно разобьется о гвард!
                    var validationResult = AbilityValidator.CheckCastPossibility(abilityIdStr, casterEntity, em);

                    if (validationResult.IsPossible)
                    {
                        // 🚀 ШАГ 2: Валидация конвейера пройдена! Только ТЕПЕРЬ проверяем, не читает ли туша спелл прямо сейчас.
                        // По канону WoW: если маг уже кастует Frostbolt и нагло жмет Frostbolt еще раз — 
                        // этот спам просто игнорируется симуляцией, не прерывая текущую полоску!
                        bool isAlreadyCasting = em.HasComponent<CastComponent>(casterEntity) && em.GetComponentData<CastComponent>(casterEntity).IsActive;

                        if (!isAlreadyCasting)
                        {
                            var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);
                            
                            // ⏳ ВЗВОД ГКД НА СЕРВЕРЕ (Изолированный чистый Си-блок)
                            if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.triggers_gcd)
                            {
                                float calculatedGcd = 1.2f; 

                                if (em.HasComponent<CombatStateComponent>(casterEntity))
                                {
                                    var combat = em.GetComponentData<CombatStateComponent>(casterEntity);
                                    
                                    combat.GcdDuration = calculatedGcd;
                                    combat.GcdRemaining = calculatedGcd;
                                    
                                    em.SetComponentData(casterEntity, combat);
                                    Debug.Log($"⏳ [CastSystem]: На боевой стейт {casterEntity} наложено ГКД: {calculatedGcd}с.");
                                }
                            } // Скобка закрылась! Блок взвода ГКД полностью изолирован.

                            // WoW-КАНОН: На старте обычного каста ману НЕ списываем! Только взводим стейт!
                            float castTime = abilityCfg != null && abilityCfg.parameters != null 
                                ? abilityCfg.parameters.cast_time 
                                : 1.7f;

                            bool isChanneling = abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.is_channeling;

                            if (!em.HasComponent<CastComponent>(casterEntity))
                            {
                                ecb.AddComponent(casterEntity, new CastComponent
                                {
                                    IsActive = true,
                                    AbilityId = request.ValueRO.AbilityId,
                                    CastTime = castTime,
                                    Progress = 0f,
                                    IsChanneling = isChanneling
                                });
                            }
                            else
                            {
                                em.SetComponentData(casterEntity, new CastComponent
                                {
                                    IsActive = true,
                                    AbilityId = request.ValueRO.AbilityId,
                                    CastTime = castTime,
                                    Progress = 0f,
                                    IsChanneling = isChanneling
                                });
                            }
                        }
                    }
                    else
                    {
                        // Сюда со свистом летят все наши варнинги о спаме кнопок во время ГКД!
                        Debug.LogWarning($"❌ [CastSystem]: Сервер отклонил старт каста '{abilityIdStr}'. Причина: {validationResult.Reason}");
                    }
                }

                // Стираем отработавший пакет-запрос из ОЗУ текущего кадра
                ecb.DestroyEntity(requestEntity);
            }

            // =========================================================================
            // ПОТОК 2: ПОКАДРОВЫЙ ТИК ТАЙМЕРОВ И ФИНИШ КАСTА (Симуляция)
            // =========================================================================
            foreach (var (castRW, entity) in SystemAPI.Query<RefRW<CastComponent>>().WithEntityAccess())
            {
                if (!castRW.ValueRO.IsActive) continue;

                var cast = castRW.ValueRW;

                // WoW-ГВАРД ДВИЖЕНИЯ: Срыв каста шагом на WASD (мана сохраняется!)
                if (!cast.IsChanneling && SystemAPI.HasComponent<MovementComponent>(entity))
                {
                    var move = SystemAPI.GetComponent<MovementComponent>(entity);
                    bool isMoving = math.lengthsq(move.Direction) > 0.001f && move.CurrentSpeed > 0.001f; 

                    if (isMoving)
                    {
                        cast.IsActive = false; 
                        cast.Progress = 0f;
                        castRW.ValueRW = cast;

                        Debug.Log($"❌ [CastSystem]: Каст заклинания '{cast.AbilityId}' СОРВАН движением. Мана сохранена.");
                        continue; 
                    }
                }

                cast.Progress += SystemAPI.Time.DeltaTime;

                // 🦾 ИСТИННЫЙ ММО-ФИНИШ: Заклинание успешно дочитано до конца!
                if (cast.Progress >= cast.CastTime)
                {
                    cast.IsActive = false; 
                    string finishedAbilityId = cast.AbilityId.ToString();

                    // Перед тем как выпустить стрелу, еще раз проверяем ману (на случай десинхрона) и списываем её!
                    var abilityCfg = AbilitiesDatabase.GetAbility(finishedAbilityId);
                    
                    if (abilityCfg != null && abilityCfg.cost != null && !string.IsNullOrEmpty(abilityCfg.cost.resource))
                    {
                        float costValue = abilityCfg.cost.value;
                        if (costValue > 0f && em.HasComponent<ResourceComponent>(entity))
                        {
                            var resources = em.GetComponentData<ResourceComponent>(entity);
                            
                            // Атомарно вычитаем Си-байты стоимости из ОЗУ чанка
                            resources.Current = math.max(0f, resources.Current - costValue);
                            em.SetComponentData(entity, resources);

                            Debug.Log($"🧪 [CastSystem]: Юнит {entity} успешно ДОКАСТОВАЛ '{finishedAbilityId}' и потратил {costValue} {abilityCfg.cost.resource}. Осталось: {resources.Current}");
                        }
                    }

                    // ⚡ ЗДЕСЬ РОЖДАЕТСЯ ExecuteSpellEvent / Вылет снаряда в CombatSystem!
                    Debug.Log($"🔥 [CastSystem]: Снаряд заклинания '{finishedAbilityId}' официально вылетел из рук {entity}!");
                }

                castRW.ValueRW = cast;
            }
        }
    }
}


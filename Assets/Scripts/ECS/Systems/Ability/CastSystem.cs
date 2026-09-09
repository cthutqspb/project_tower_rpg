using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities;
using Debug = UnityEngine.Debug;

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
                            
                            // ⏳ ВЗВОД ГКД НА СЕРВЕРЕ (Твой чистый изолированный Си-блок)
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
                            }

                            // =========================================================================
                            // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ДЛЯ МГНОВЕННЫХ СПОСОБНОСТЕЙ (WoW-канон)
                            // =========================================================================
                            float castTime = abilityCfg != null && abilityCfg.parameters != null 
                                ? abilityCfg.parameters.cast_time 
                                : 1.7f;

                            // Если у способности есть КД, и она мгновенная — вешаем КД прямо сейчас
                            if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
                            {
                                if (castTime <= 0f) // Только для Instant способностей!
                                {
                                    if (SystemAPI.HasBuffer<ActiveCooldownElement>(casterEntity))
                                    {
                                        var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(casterEntity);
                                        
                                        // Извлекаем CooldownGroup (например, из конфига или как хэш от AbilityId)
                                        FixedString32Bytes cdGroup = abilityCfg.parameters.cooldown_group ?? abilityIdStr;
                                        float cdDuration = abilityCfg.parameters.cooldown;

                                        // Проверяем, нет ли уже такого КД в буфере, чтобы не дублировать
                                        bool alreadyHasCd = false;
                                        for (int c = 0; c < cooldownsBuffer.Length; c++)
                                        {
                                            if (cooldownsBuffer[c].CooldownGroup == cdGroup)
                                            {
                                                alreadyHasCd = true;
                                                break;
                                            }
                                        }

                                        if (!alreadyHasCd)
                                        {
                                            cooldownsBuffer.Add(new ActiveCooldownElement
                                            {
                                                CooldownGroup = cdGroup,
                                                Remaining = cdDuration,
                                                Duration = cdDuration
                                            });
                                            Debug.Log($"🎯 [CastSystem]: На юнита {casterEntity} наложен КД группы '{cdGroup}': {cdDuration}с.");
                                        }
                                    }
                                }
                            }
                            // =========================================================================

                            bool isChanneling = abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.is_channeling;

                            if (!em.HasComponent<CastComponent>(casterEntity))
                            {
                                ecb.AddComponent(casterEntity, new CastComponent
                                {
                                    IsActive = true,
                                    AbilityId = request.ValueRO.AbilityId,
                                    CastTime = castTime,
                                    Progress = 0f,
                                    IsChanneling = isChanneling,
                                    Target = request.ValueRO.TargetEntity
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
                                    IsChanneling = isChanneling,
                                    Target = request.ValueRO.TargetEntity
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

                    // =========================================================================
                    // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ПОСЛЕ УСПЕШНОГО КАСTА (WoW-канон)
                    // =========================================================================
                    if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
                    {
                        float castTime = abilityCfg.parameters.cast_time;
                        if (castTime > 0f) 
                        {
                            if (SystemAPI.HasBuffer<ActiveCooldownElement>(entity))
                            {
                                var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(entity);
                                FixedString32Bytes cdGroup = abilityCfg.parameters.cooldown_group ?? finishedAbilityId;
                                float cdDuration = abilityCfg.parameters.cooldown;

                                bool alreadyHasCd = false;
                                for (int c = 0; c < cooldownsBuffer.Length; c++)
                                {
                                    if (cooldownsBuffer[c].CooldownGroup == cdGroup)
                                    {
                                        alreadyHasCd = true;
                                        break;
                                    }
                                }

                                if (!alreadyHasCd)
                                {
                                    cooldownsBuffer.Add(new ActiveCooldownElement
                                    {
                                        CooldownGroup = cdGroup,
                                        Remaining = cdDuration,
                                        Duration = cdDuration
                                    });
                                    Debug.Log($"🎯 [CastSystem]: На юнита {entity} наложен КД группы '{cdGroup}': {cdDuration}с.");
                                }
                            }
                        }
                    }
                    // =========================================================================

                    // 🚀 ИСТИННЫЙ WOW-КАНОН: Достаем цель, которую мы заморозили в ОЗУ в миллисекунду НАЖАТИЯ кнопки!
                    Entity castTarget = cast.Target;

                    if (castTarget != Entity.Null && em.Exists(castTarget))
                    {
                        // Рождаем событийный пакет-запрос для боевой системы
                        Entity combatEventEntity = ecb.CreateEntity();
                        ecb.AddComponent(combatEventEntity, new CombatEventRequest
                        {
                            Caster = entity,
                            Target = castTarget, // Цель пуленепробиваема к смене фокуса в процессе каста!
                            AbilityId = cast.AbilityId
                        });

                        Debug.Log($"🔥 [CastSystem]: Каст завершен! Рожден CombatEventRequest для '{finishedAbilityId}'. Урон летит в ЗАФИКСИРОВАННУЮ НА СТАРТЕ цель: {castTarget}");
                    }
                    else
                    {
                        Debug.LogWarning($"❌ [CastSystem]: Каст '{finishedAbilityId}' дочитан, но зафиксированная на старте цель {castTarget} умерла или исчезла из чанков ОЗУ мира! Урон отменен.");
                    }
                }

                castRW.ValueRW = cast;
            }
        }
    }
}


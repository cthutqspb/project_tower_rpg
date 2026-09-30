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

            // 🦾 ШИННЫЙ ГВАРД: Находим синглтон-сущность нашего буфера презентационных событий
            Entity eventBufferSingleton = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var bufferEntity))
            {
                eventBufferSingleton = bufferEntity;
            }

            // =========================================================================
            // ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Серверный шлюз на старте)
            // =========================================================================
            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CastRequest>>().WithEntityAccess())
            {
                Entity casterEntity = request.ValueRO.Caster;
                string abilityIdStr = request.ValueRO.AbilityId.ToString();

                if (casterEntity != Entity.Null && em.Exists(casterEntity))
                {
                    var validationResult = AbilityValidator.CheckCastPossibility(abilityIdStr, casterEntity, em);

                    if (validationResult.IsPossible)
                    {
                        bool isAlreadyCasting = em.HasComponent<CastComponent>(casterEntity) && em.GetComponentData<CastComponent>(casterEntity).IsActive;

                        if (!isAlreadyCasting)
                        {
                            var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);
                            
                            // ⏳ ВЗВОД ГКД НА СЕРВЕРЕ
                            if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.triggers_gcd)
                            {
                                float calculatedGcd = 1.2f; 

                                if (em.HasComponent<CombatStateComponent>(casterEntity))
                                {
                                    var combatState = em.GetComponentData<CombatStateComponent>(casterEntity);
                                    combatState.GcdDuration = calculatedGcd;
                                    combatState.GcdRemaining = calculatedGcd;
                                    em.SetComponentData(casterEntity, combatState);
                                    Debug.Log($"⏳ [CastSystem]: На боевой стейт {casterEntity} наложено ГКД: {calculatedGcd}с.");
                                }
                            }

                            // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ДЛЯ МГНОВЕННЫХ СПОСОБНОСТЕЙ
                            float castTime = abilityCfg != null && abilityCfg.parameters != null 
                                ? abilityCfg.parameters.cast_time 
                                : 1.7f;

                            if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
                            {
                                if (castTime <= 0f) 
                                {
                                    if (SystemAPI.HasBuffer<ActiveCooldownElement>(casterEntity))
                                    {
                                        var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(casterEntity);
                                        FixedString32Bytes cooldownGroup = string.IsNullOrEmpty(abilityCfg.parameters.cooldown_group) 
                                            ? abilityIdStr
                                            : abilityCfg.parameters.cooldown_group;

                                        float cooldownDuration = abilityCfg.parameters.cooldown;

                                        bool alreadyHasCooldown = false;
                                        for (int i = 0; i < cooldownsBuffer.Length; i++)
                                        {
                                            if (cooldownsBuffer[i].CooldownGroup == cooldownGroup)
                                            {
                                                alreadyHasCooldown = true;
                                                break;
                                            }
                                        }

                                        if (!alreadyHasCooldown)
                                        {
                                            cooldownsBuffer.Add(new ActiveCooldownElement
                                            {
                                                CooldownGroup = cooldownGroup,
                                                Remaining = cooldownDuration,
                                                Duration = cooldownDuration
                                            });
                                            Debug.Log($"🎯 [CastSystem]: На юнита {casterEntity} наложен КД группы '{cooldownGroup}': {cooldownDuration}с.");
                                        }
                                    }
                                }
                            }

                            bool isChanneling = abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.is_channeling;

                            // 🦾 ВЗВОД СТEЙТА В ЧАНКЕ ПАМЯТИ ECS
                            var newCastData = new CastComponent
                            {
                                IsActive = true,
                                AbilityId = request.ValueRO.AbilityId,
                                CastTime = castTime,
                                Progress = 0f,
                                IsChanneling = isChanneling,
                                Target = request.ValueRO.TargetEntity
                            };

                            if (!em.HasComponent<CastComponent>(casterEntity))
                            {
                                ecb.AddComponent(casterEntity, newCastData);
                            }
                            else
                            {
                                em.SetComponentData(casterEntity, newCastData);
                            }

                            // =========================================================================
                            // 🚀 ТРАНСЛЯЦИЯ СОБЫТИЯ В КЛИЕНТСКИЙ АНИМАТОР (Событийная шина Дипсика!)
                            // =========================================================================
                            if (eventBufferSingleton != Entity.Null)
                            {
                                // Мгновенно вычисляем: это инстант-атака или запуск длинного заклинания?
                                PresentationEventKind eventKind = castTime <= 0f 
                                    ? PresentationEventKind.Attack 
                                    : PresentationEventKind.CastStart;

                                // 🦾 СИ-ФИКС: Передаем экземпляр PresentationEvent прямо в аргументы AppendToBuffer!
                                ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
                                {
                                    Kind = eventKind,
                                    Source = casterEntity,
                                    Target = request.ValueRO.TargetEntity,
                                    Param = request.ValueRO.AbilityId // Передаем FixedString спелла ("frostbolt")
                                });

                                Debug.Log($"📡 [CastSystem]: В шину презентации улетело событие {eventKind} от юнита {casterEntity.Index}.");
                            }

                        }
                    }
                    else
                    {
                        Debug.LogWarning($"❌ [CastSystem]: Сервер отклонил старт каста '{abilityIdStr}'. Причина: {validationResult.Reason}");
                    }
                }

                // Очищаем отработанную сущность запроса
                ecb.DestroyEntity(requestEntity);
            }

            // =========================================================================
            // ПОТОК 2: ПОКАДРОВЫЙ ТИК ТАЙМЕРОВ И ФИНИШ КАСTА (Симуляция)
            // =========================================================================
            foreach (var (castRW, entity) in SystemAPI.Query<RefRW<CastComponent>>().WithEntityAccess())
            {
                if (!castRW.ValueRO.IsActive) continue;

                var cast = castRW.ValueRW;
                bool shouldSendCastEnd = false; // Флаг-замок для единой отправки визуала!

                // WoW-ГВАРД ДВИЖЕНИЯ: Срыв каста шагом на WASD
                if (!cast.IsChanneling && SystemAPI.HasComponent<MovementComponent>(entity))
                {
                    var movement = SystemAPI.GetComponent<MovementComponent>(entity);
                    bool isMoving = math.lengthsq(movement.Direction) > 0.001f && movement.CurrentSpeed > 0.001f; 

                    if (isMoving)
                    {
                        cast.IsActive = false; 
                        cast.Progress = 0f;
                        castRW.ValueRW = cast;
                        
                        // Взводим флаг отправки визуала и прыгаем в финальный шлюз кадра
                        shouldSendCastEnd = true;
                        Debug.Log($"❌ [CastSystem]: Каст заклинания '{cast.AbilityId}' СОРВАН движением.");
                    }
                }

                if (!cast.IsActive && shouldSendCastEnd)
                {
                    if (eventBufferSingleton != Entity.Null)
                    {
                        ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
                        {
                            Kind = PresentationEventKind.CastEnd,
                            Source = entity,
                            Target = cast.Target,
                            Param = cast.AbilityId
                        });
                    }
                    continue;
                }

                cast.Progress += SystemAPI.Time.DeltaTime;

                // 🦾 ИСТИННЫЙ ММО-ФИНИШ: Заклинание успешно дочитано до конца!
                if (cast.Progress >= cast.CastTime)
                {
                    cast.IsActive = false; 
                    shouldSendCastEnd = true; // Финиш — это всегда конец анимации каста
                    string abilityIdStr = cast.AbilityId.ToString();

                    var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);
                    
                    if (abilityCfg != null && abilityCfg.cost != null && !string.IsNullOrEmpty(abilityCfg.cost.resource))
                    {
                        float costValue = abilityCfg.cost.value;
                        if (costValue > 0f && em.HasComponent<ResourceComponent>(entity))
                        {
                            var resources = em.GetComponentData<ResourceComponent>(entity);
                            resources.Current = math.max(0f, resources.Current - costValue);
                            em.SetComponentData(entity, resources);

                            Debug.Log($"🧪 [CastSystem]: Юнит {entity} потратил {costValue} {abilityCfg.cost.resource}.");
                        }
                    }

                    // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ПОСЛЕ УСПЕШНОГО КАСTА (WoW-канон)
                    if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
                    {
                        if (abilityCfg.parameters.cast_time > 0f) 
                        {
                            if (SystemAPI.HasBuffer<ActiveCooldownElement>(entity))
                            {
                                var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(entity);
                                
                                // Выпрямили тернарную проверку структуры FixedString32Bytes
                                FixedString32Bytes cooldownGroup = string.IsNullOrEmpty(abilityCfg.parameters.cooldown_group) 
                                    ? cast.AbilityId 
                                    : (FixedString32Bytes)abilityCfg.parameters.cooldown_group;
                                    
                                float cooldownDuration = abilityCfg.parameters.cooldown;

                                bool alreadyHasCooldown = false;
                                for (int i = 0; i < cooldownsBuffer.Length; i++)
                                {
                                    if (cooldownsBuffer[i].CooldownGroup == cooldownGroup)
                                    {
                                        alreadyHasCooldown = true;
                                        break;
                                    }
                                }

                                if (!alreadyHasCooldown)
                                {
                                    cooldownsBuffer.Add(new ActiveCooldownElement
                                    {
                                        CooldownGroup = cooldownGroup,
                                        Remaining = cooldownDuration,
                                        Duration = cooldownDuration
                                    });
                                }
                            }
                        }
                    }

                    Entity castTarget = cast.Target;

                    if (castTarget != Entity.Null && em.Exists(castTarget))
                    {
                        Entity combatEventEntity = ecb.CreateEntity();
                        ecb.AddComponent(combatEventEntity, new CombatEventRequest
                        {
                            Caster = entity,
                            Target = castTarget, 
                            AbilityId = cast.AbilityId
                        });

                        Debug.Log($"🔥 [CastSystem]: Каст завершен для '{abilityIdStr}'. Запрос отправлен.");
                    }
                    else
                    {
                        Debug.LogWarning($"❌ [CastSystem]: Цель умерла! Урон отменен.");
                    }
                }

                // 🌐 ЕДИНЫЙ СЕРВЕРНЫЙ ШЛЮЗ ВИЗУАЛА: отправляем CastEnd ровно ОДИН раз при любом исходе финиша!
                if (shouldSendCastEnd && eventBufferSingleton != Entity.Null)
                {
                    ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
                    {
                        Kind = PresentationEventKind.CastEnd,
                        Source = entity,
                        Target = cast.Target,
                        Param = cast.AbilityId
                    });
                }

                castRW.ValueRW = cast;
            }
        }
    }
}


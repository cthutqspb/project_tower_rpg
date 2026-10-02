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
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);

            Entity eventBuffer = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity< PresentationEventBufferTag >(out var bufferEntity))
            {
                eventBuffer = bufferEntity;
            }

            // =========================================================================
            // 🦾 ЧИСТЫЙ СИ-КОНВЕЙЕР: ДВА ЛОГИЧЕСКИХ ПОТОКА
            // =========================================================================
            ProcessRequests(em, ecb, eventBuffer);
            UpdateCasts(em, ecb, eventBuffer);
        }

        // =========================================================================
        // 📥 ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Стартер)
        // =========================================================================
        private void ProcessRequests(EntityManager em, EntityCommandBuffer ecb, Entity eventBuffer)
        {
            foreach (var (request, requestEntity) in SystemAPI.Query< RefRO< CastRequest > >().WithEntityAccess())
            {
                Entity caster = request.ValueRO.Caster;
                string abilityId = request.ValueRO.AbilityId.ToString();

                if (caster == Entity.Null || !em.Exists(caster))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                var validation = AbilityValidator.CheckCastPossibility(abilityId, caster, em);

                if (validation.IsPossible)
                {
                    bool isAlreadyCasting = em.HasComponent< CastComponent >(caster) 
                                            && em.GetComponentData< CastComponent >(caster).IsActive;

                    if (!isAlreadyCasting)
                    {
                        var cfg = AbilitiesDatabase.GetAbility(abilityId);
                        float castTime = cfg?.parameters != null ? cfg.parameters.cast_time : 1.7f;

                        // Взводим спам-защиту (ГКД и КД для инстантов)
                        ApplyStartCooldowns(em, caster, cfg, abilityId, castTime);

                        bool isChanneling = cfg?.parameters != null && cfg.parameters.is_channeling;

                        var newCastData = new CastComponent
                        {
                            IsActive = true,
                            AbilityId = request.ValueRO.AbilityId,
                            CastTime = castTime,
                            Progress = 0f,
                            IsChanneling = isChanneling,
                            Target = request.ValueRO.TargetEntity
                        };

                        if (!em.HasComponent< CastComponent >(caster))
                            ecb.AddComponent(caster, newCastData);
                        else
                            em.SetComponentData(caster, newCastData);

                        // Трансляция события старта в шину аниматора
                        SendCastStartEvent(ecb, eventBuffer, caster, request.ValueRO.TargetEntity, request.ValueRO.AbilityId, castTime);
                    }
                }
                else
                {
                    Debug.LogWarning($"❌ [CastSystem]: Сервер отклонил старт каста '{abilityId}'. Причина: {validation.Reason}");
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        // =========================================================================
        // 🔄 ПОТОК 2: ПОКАДРОВАЯ СИМУЛЯЦИЯ И ФИНИШИ (Ядро)
        // =========================================================================
                private void UpdateCasts(EntityManager em, EntityCommandBuffer ecb, Entity eventBuffer)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (castRW, entity) in SystemAPI.Query< RefRW< CastComponent > >().WithEntityAccess())
            {
                // 🦾 ГВАРД-ОТСЕКАТЕЛЬ: Если каст уже выключен, система вообще мгновенно 
                // скипает эту сущность, никогда не спамя повторные ивенты!
                if (!castRW.ValueRO.IsActive) continue;

                var cast = castRW.ValueRW;
                bool shouldSendCastEnd = false;

                // WoW-ГВАРД ДВИЖЕНИЯ: Срыв каста шагом на WASD
                if (!cast.IsChanneling && SystemAPI.HasComponent< MovementComponent >(entity))
                {
                    var movement = SystemAPI.GetComponent< MovementComponent >(entity);
                    bool isMoving = math.lengthsq(movement.Direction) > 0.001f && movement.CurrentSpeed > 0.001f;

                    // Инстант-касты на ходу (CastTime <= 0) шёлково игнорируют срыв!
                    if (isMoving && cast.CastTime > 0.01f)
                    {
                        cast.IsActive = false; // МЫ НАМЕРТВО ВЫКЛЮЧАЕМ АКТИВНОСТЬ ТУТ!
                        cast.Progress = 0f;
                        
                        shouldSendCastEnd = true;
                        Debug.Log($"❌ [CastSystem]: Каст заклинания '{cast.AbilityId}' СОРВАН движением.");
                    }
                }

                // 🪓 ВСЕ УБЛЮДСКИЕ ПРОМЕЖУТОЧНЫЕ IF С CONTINUE ВЫЖЖЕНЫ В НОЛЬ!

                // Если каст выжил после проверки движения — качаем таймер симуляции вперед
                if (cast.IsActive)
                {
                    cast.Progress += dt;

                    // 🏁 УСПЕШНЫЙ ММО-ФИНИШ: Заклинание полностью дочитано!
                    if (cast.Progress >= cast.CastTime)
                    {
                        cast.IsActive = false; // МЫ НАМЕРТВО ВЫКЛЮЧАЕМ АКТИВНОСТЬ ТУТ!
                        shouldSendCastEnd = true;

                        string abilityId = cast.AbilityId.ToString();
                        var cfg = AbilitiesDatabase.GetAbility(abilityId);

                        // Списываем стоимость ресурсов
                        ConsumeResourceCost(em, entity, cfg);

                        // Взвод обычного КД после успешного каста (WoW-канон)
                        ApplyPostCastCooldown(entity, cfg, cast.AbilityId);

                        // Рождаем payload (снаряд или мгновенный комбат-хит)
                        ExecuteAbilityPayload(em, ecb, entity, cast, cfg, abilityId);
                    }
                }

                // 🌐 ЕДИНЫЙ СТEРИЛЬНЫЙ ШЛЮЗ ВИЗУАЛА: 
                // Срабатывает СТРОГО ОДИН РАЗ в момент переключения cast.IsActive из true в false!
                if (shouldSendCastEnd && eventBuffer != Entity.Null)
                {
                    SendCastEndEvent(ecb, eventBuffer, entity, cast.Target, cast.AbilityId);
                }

                // Намертво сохраняем измененный стейт компонента в чанк ОЗУ.
                // На следующем кадре `if (!castRW.ValueRO.IsActive) continue;` шёлково отсечёт этот юнит.
                castRW.ValueRW = cast;
            }
        }

        // =========================================================================
        // 🛠️ ВСПОМОГАТЕЛЬНЫЕ ПОДМЕТОДЫ СИ-КОНВЕЙЕРА
        // =========================================================================

        private void ApplyStartCooldowns(EntityManager em, Entity caster, AbilityConfig cfg, string idStr, float castTime)
        {
            if (cfg?.parameters == null) return;

            if (cfg.parameters.triggers_gcd && em.HasComponent< CombatStateComponent >(caster))
            {
                float gcd = 1.2f;
                var combatState = em.GetComponentData< CombatStateComponent >(caster);
                combatState.GcdDuration = gcd;
                combatState.GcdRemaining = gcd;
                em.SetComponentData(caster, combatState);
                Debug.Log($"⏳ [CastSystem]: На боевой стейт {caster} наложено ГКД: {gcd}с.");
            }

            if (castTime <= 0f && cfg.parameters.cooldown > 0f && SystemAPI.HasBuffer< ActiveCooldownElement >(caster))
            {
                var cooldowns = SystemAPI.GetBuffer< ActiveCooldownElement >(caster);
                FixedString32Bytes group = string.IsNullOrEmpty(cfg.parameters.cooldown_group) ? idStr : cfg.parameters.cooldown_group;

                if (!HasCooldown(cooldowns, group))
                {
                    cooldowns.Add(new ActiveCooldownElement { CooldownGroup = group, Remaining = cfg.parameters.cooldown, Duration = cfg.parameters.cooldown });
                    Debug.Log($"🎯 [CastSystem]: На юнита {caster} наложен КД группы '{group}': {cfg.parameters.cooldown}с.");
                }
            }
        }

        private void ApplyPostCastCooldown(Entity entity, AbilityConfig cfg, FixedString32Bytes abilityId)
        {
            if (cfg?.parameters == null || cfg.parameters.cooldown <= 0f || cfg.parameters.cast_time <= 0f) return;

            if (SystemAPI.HasBuffer< ActiveCooldownElement >(entity))
            {
                var cooldowns = SystemAPI.GetBuffer< ActiveCooldownElement >(entity);
                FixedString32Bytes group = string.IsNullOrEmpty(cfg.parameters.cooldown_group) ? abilityId : (FixedString32Bytes)cfg.parameters.cooldown_group;

                if (!HasCooldown(cooldowns, group))
                {
                    cooldowns.Add(new ActiveCooldownElement { CooldownGroup = group, Remaining = cfg.parameters.cooldown, Duration = cfg.parameters.cooldown });
                }
            }
        }

                private void ConsumeResourceCost(EntityManager em, Entity entity, AbilityConfig cfg)
        {
            if (cfg?.cost == null || string.IsNullOrEmpty(cfg.cost.resource) || cfg.cost.value <= 0f) return;

            if (em.HasComponent< ResourceComponent >(entity))
            {
                var resources = em.GetComponentData< ResourceComponent >(entity);
                resources.Current = math.max(0f, resources.Current - cfg.cost.value);
                em.SetComponentData(entity, resources);
                Debug.Log($"🧪 [CastSystem]: Юнит {entity} потратил {cfg.cost.value} {cfg.cost.resource}.");
            }
        }

        private void ExecuteAbilityPayload(EntityManager em, EntityCommandBuffer ecb, Entity entity, CastComponent cast, AbilityConfig cfg, string idStr)
        {
            Entity castTarget = cast.Target;

            if (cfg?.parameters != null && !cfg.parameters.requires_target)
            {
                castTarget = entity;
            }

            if (castTarget != Entity.Null && em.Exists(castTarget))
            {
                bool isProjectile = cfg?.delivery != null && cfg.delivery.type == "projectile";

                if (isProjectile)
                {
                    string rawPath = cfg.delivery.fx?.prefab_path ?? "Projectiles/frostbolt_debug";
                    
                    // 🦾 СИ-ФИКС: Создаем РОВНО ОДНУ сущность запроса спавна снаряда!
                    Entity req = ecb.CreateEntity();
                    ecb.AddComponent(req, new ProjectileSpawnRequest 
                    { 
                        CasterEntity = entity, 
                        TargetEntity = castTarget, 
                        AbilityId = cast.AbilityId, 
                        PrefabPath = rawPath 
                    });
                    
                    Debug.Log($"✉️ [CastSystem]: ProjectileSpawnRequest выписан с префабом '{rawPath}'.");
                }
                else
                {
                    Entity combatEvent = ecb.CreateEntity();
                    ecb.AddComponent(combatEvent, new CombatEventRequest 
                    { 
                        Caster = entity, 
                        Target = castTarget, 
                        AbilityId = cast.AbilityId 
                    });
                    
                    Debug.Log($"⚔️ [CastSystem]: Мгновенный хит способности '{idStr}'. Отправлен CombatEventRequest на цель {castTarget.Index}.");
                }
            }
            else
            {
                Debug.LogWarning($"❌ [CastSystem]: Способность '{cast.AbilityId}' прервана — нет легитимной цели!");
            }
        }

        private bool HasCooldown(DynamicBuffer< ActiveCooldownElement > buffer, FixedString32Bytes group)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].CooldownGroup == group) return true;
            }
            return false;
        }

        private void SendCastStartEvent(EntityCommandBuffer ecb, Entity eventBuffer, Entity caster, Entity target, FixedString32Bytes abilityId, float castTime)
        {
            if (eventBuffer == Entity.Null) return;
            PresentationEventKind kind = castTime <= 0f ? PresentationEventKind.Attack : PresentationEventKind.CastStart;
            ecb.AppendToBuffer(eventBuffer, new PresentationEvent { Kind = kind, Source = caster, Target = target, Param = abilityId });
            Debug.Log($"📡 [CastSystem]: В шину презентации улетело событие {kind} от юнита {caster.Index}.");
        }

        private void SendCastEndEvent(EntityCommandBuffer ecb, Entity eventBuffer, Entity caster, Entity target, FixedString32Bytes abilityId)
        {
            if (eventBuffer == Entity.Null) return;
            ecb.AppendToBuffer(eventBuffer, new PresentationEvent { Kind = PresentationEventKind.CastEnd, Source = caster, Target = target, Param = abilityId });
        }
    }
}


// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using ProjectTowerRpg.ECS.Components;
// using ProjectTowerRpg.Core.Abilities;
// using Debug = UnityEngine.Debug;
//
// namespace ProjectTowerRpg.ECS.Systems
// {
//     [UpdateInGroup(typeof(SimulationSystemGroup))]
//     public partial class CastSystem : SystemBase
//     {
//
//         protected override void OnUpdate()
//         {
//             var em = EntityManager;
//             var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
//
//             //var projectileDatabase = SystemAPI.GetSingleton<ProjectileDatabase>();
//             //if (!SystemAPI.TryGetSingleton<ProjectileDatabase>(out var projectileDatabase)) return;
//
//
//             // 🦾 ШИННЫЙ ГВАРД: Находим синглтон-сущность нашего буфера презентационных событий
//             Entity eventBufferSingleton = Entity.Null;
//             if (SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var bufferEntity))
//             {
//                 eventBufferSingleton = bufferEntity;
//             }
//
//             // =========================================================================
//             // ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Серверный шлюз на старте)
//             // =========================================================================
//             foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CastRequest>>().WithEntityAccess())
//             {
//                 Entity casterEntity = request.ValueRO.Caster;
//                 string abilityIdStr = request.ValueRO.AbilityId.ToString();
//
//                 if (casterEntity != Entity.Null && em.Exists(casterEntity))
//                 {
//                     var validationResult = AbilityValidator.CheckCastPossibility(abilityIdStr, casterEntity, em);
//
//                     if (validationResult.IsPossible)
//                     {
//                         bool isAlreadyCasting = em.HasComponent<CastComponent>(casterEntity) && em.GetComponentData<CastComponent>(casterEntity).IsActive;
//
//                         if (!isAlreadyCasting)
//                         {
//                             var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);
//                             
//                             // ⏳ ВЗВОД ГКД НА СЕРВЕРЕ
//                             if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.triggers_gcd)
//                             {
//                                 float calculatedGcd = 1.2f; 
//
//                                 if (em.HasComponent<CombatStateComponent>(casterEntity))
//                                 {
//                                     var combatState = em.GetComponentData<CombatStateComponent>(casterEntity);
//                                     combatState.GcdDuration = calculatedGcd;
//                                     combatState.GcdRemaining = calculatedGcd;
//                                     em.SetComponentData(casterEntity, combatState);
//                                     Debug.Log($"⏳ [CastSystem]: На боевой стейт {casterEntity} наложено ГКД: {calculatedGcd}с.");
//                                 }
//                             }
//
//                             // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ДЛЯ МГНОВЕННЫХ СПОСОБНОСТЕЙ
//                             float castTime = abilityCfg != null && abilityCfg.parameters != null 
//                                 ? abilityCfg.parameters.cast_time 
//                                 : 1.7f;
//
//                             if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
//                             {
//                                 if (castTime <= 0f) 
//                                 {
//                                     if (SystemAPI.HasBuffer<ActiveCooldownElement>(casterEntity))
//                                     {
//                                         var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(casterEntity);
//                                         FixedString32Bytes cooldownGroup = string.IsNullOrEmpty(abilityCfg.parameters.cooldown_group) 
//                                             ? abilityIdStr
//                                             : abilityCfg.parameters.cooldown_group;
//
//                                         float cooldownDuration = abilityCfg.parameters.cooldown;
//
//                                         bool alreadyHasCooldown = false;
//                                         for (int i = 0; i < cooldownsBuffer.Length; i++)
//                                         {
//                                             if (cooldownsBuffer[i].CooldownGroup == cooldownGroup)
//                                             {
//                                                 alreadyHasCooldown = true;
//                                                 break;
//                                             }
//                                         }
//
//                                         if (!alreadyHasCooldown)
//                                         {
//                                             cooldownsBuffer.Add(new ActiveCooldownElement
//                                             {
//                                                 CooldownGroup = cooldownGroup,
//                                                 Remaining = cooldownDuration,
//                                                 Duration = cooldownDuration
//                                             });
//                                             Debug.Log($"🎯 [CastSystem]: На юнита {casterEntity} наложен КД группы '{cooldownGroup}': {cooldownDuration}с.");
//                                         }
//                                     }
//                                 }
//                             }
//
//                             bool isChanneling = abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.is_channeling;
//
//                             // 🦾 ВЗВОД СТEЙТА В ЧАНКЕ ПАМЯТИ ECS
//                             var newCastData = new CastComponent
//                             {
//                                 IsActive = true,
//                                 AbilityId = request.ValueRO.AbilityId,
//                                 CastTime = castTime,
//                                 Progress = 0f,
//                                 IsChanneling = isChanneling,
//                                 Target = request.ValueRO.TargetEntity
//                             };
//
//                             if (!em.HasComponent<CastComponent>(casterEntity))
//                             {
//                                 ecb.AddComponent(casterEntity, newCastData);
//                             }
//                             else
//                             {
//                                 em.SetComponentData(casterEntity, newCastData);
//                             }
//
//                             // =========================================================================
//                             // 🚀 ТРАНСЛЯЦИЯ СОБЫТИЯ В КЛИЕНТСКИЙ АНИМАТОР (Событийная шина Дипсика!)
//                             // =========================================================================
//                             if (eventBufferSingleton != Entity.Null)
//                             {
//                                 // Мгновенно вычисляем: это инстант-атака или запуск длинного заклинания?
//                                 PresentationEventKind eventKind = castTime <= 0f 
//                                     ? PresentationEventKind.Attack 
//                                     : PresentationEventKind.CastStart;
//
//                                 // 🦾 СИ-ФИКС: Передаем экземпляр PresentationEvent прямо в аргументы AppendToBuffer!
//                                 ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
//                                 {
//                                     Kind = eventKind,
//                                     Source = casterEntity,
//                                     Target = request.ValueRO.TargetEntity,
//                                     Param = request.ValueRO.AbilityId // Передаем FixedString спелла ("frostbolt")
//                                 });
//
//                                 Debug.Log($"📡 [CastSystem]: В шину презентации улетело событие {eventKind} от юнита {casterEntity.Index}.");
//                             }
//
//                         }
//                     }
//                     else
//                     {
//                         Debug.LogWarning($"❌ [CastSystem]: Сервер отклонил старт каста '{abilityIdStr}'. Причина: {validationResult.Reason}");
//                     }
//                 }
//
//                 // Очищаем отработанную сущность запроса
//                 ecb.DestroyEntity(requestEntity);
//             }
//
//             // =========================================================================
//             // ПОТОК 2: ПОКАДРОВЫЙ ТИК ТАЙМЕРОВ И ФИНИШ КАСTА (Симуляция)
//             // =========================================================================
//             foreach (var (castRW, entity) in SystemAPI.Query<RefRW<CastComponent>>().WithEntityAccess())
//             {
//                 if (!castRW.ValueRO.IsActive) continue;
//
//                 var cast = castRW.ValueRW;
//                 bool shouldSendCastEnd = false; // Флаг-замок для единой отправки визуала!
//
//                 // WoW-ГВАРД ДВИЖЕНИЯ: Срыв каста шагом на WASD
//                 if (!cast.IsChanneling && SystemAPI.HasComponent<MovementComponent>(entity))
//                 {
//                     var movement = SystemAPI.GetComponent<MovementComponent>(entity);
//                     bool isMoving = math.lengthsq(movement.Direction) > 0.001f && movement.CurrentSpeed > 0.001f; 
//
//                     if (isMoving && cast.CastTime > 0.01f)
//                     {
//                         cast.IsActive = false; 
//                         cast.Progress = 0f;
//                         castRW.ValueRW = cast;
//                         
//                         shouldSendCastEnd = true;
//                         Debug.Log($"❌ [CastSystem]: Каст заклинания '{cast.AbilityId}' СОРВАН движением.");
//                     }
//                 }
//
//                 if (!cast.IsActive && shouldSendCastEnd)
//                 {
//                     if (eventBufferSingleton != Entity.Null)
//                     {
//                         ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
//                         {
//                             Kind = PresentationEventKind.CastEnd,
//                             Source = entity,
//                             Target = cast.Target,
//                             Param = cast.AbilityId
//                         });
//                     }
//                     continue;
//                 }
//
//                 cast.Progress += SystemAPI.Time.DeltaTime;
//
//                 // 🦾 ИСТИННЫЙ ММО-ФИНИШ: Заклинание успешно дочитано до конца!
//                 if (cast.Progress >= cast.CastTime)
//                 {
//                     cast.IsActive = false; 
//                     shouldSendCastEnd = true; // Финиш — это всегда конец анимации каста
//                     string abilityIdStr = cast.AbilityId.ToString();
//
//                     var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);
//                     
//                     if (abilityCfg != null && abilityCfg.cost != null && !string.IsNullOrEmpty(abilityCfg.cost.resource))
//                     {
//                         float costValue = abilityCfg.cost.value;
//                         if (costValue > 0f && em.HasComponent<ResourceComponent>(entity))
//                         {
//                             var resources = em.GetComponentData<ResourceComponent>(entity);
//                             resources.Current = math.max(0f, resources.Current - costValue);
//                             em.SetComponentData(entity, resources);
//
//                             Debug.Log($"🧪 [CastSystem]: Юнит {entity} потратил {costValue} {abilityCfg.cost.resource}.");
//                         }
//                     }
//
//                     // 🎯 ВЗВОД ОБЫЧНОГО КУЛДАУНА ПОСЛЕ УСПЕШНОГО КАСTА (WoW-канон)
//                     if (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.cooldown > 0f)
//                     {
//                         if (abilityCfg.parameters.cast_time > 0f) 
//                         {
//                             if (SystemAPI.HasBuffer<ActiveCooldownElement>(entity))
//                             {
//                                 var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(entity);
//                                 
//                                 // Выпрямили тернарную проверку структуры FixedString32Bytes
//                                 FixedString32Bytes cooldownGroup = string.IsNullOrEmpty(abilityCfg.parameters.cooldown_group) 
//                                     ? cast.AbilityId 
//                                     : (FixedString32Bytes)abilityCfg.parameters.cooldown_group;
//                                     
//                                 float cooldownDuration = abilityCfg.parameters.cooldown;
//
//                                 bool alreadyHasCooldown = false;
//                                 for (int i = 0; i < cooldownsBuffer.Length; i++)
//                                 {
//                                     if (cooldownsBuffer[i].CooldownGroup == cooldownGroup)
//                                     {
//                                         alreadyHasCooldown = true;
//                                         break;
//                                     }
//                                 }
//
//                                 if (!alreadyHasCooldown)
//                                 {
//                                     cooldownsBuffer.Add(new ActiveCooldownElement
//                                     {
//                                         CooldownGroup = cooldownGroup,
//                                         Remaining = cooldownDuration,
//                                         Duration = cooldownDuration
//                                     });
//                                 }
//                             }
//                         }
//                     }
//
//                     // =========================================================================
//                     // 🎯 ОПРЕДЕЛЕНИЕ ИСТИННОЙ ЦЕЛИ СПОСОБНОСТИ
//                     // =========================================================================
//                     Entity castTarget = cast.Target;
//
//                     if (abilityCfg != null && abilityCfg.parameters != null)
//                     {
//                         // WoW-КАНОН: Если способность НЕ требует цели (селф-бафф),
//                         // её геймплейной целью автоматически становится сам кастер (Игрок)!
//                         if (!abilityCfg.parameters.requires_target)
//                         {
//                             castTarget = entity;
//                         }
//                     }
//
//                     if (castTarget != Entity.Null && em.Exists(castTarget))
//                     {
//                         string completedAbilityId = cast.AbilityId.ToString();
//                         abilityCfg = AbilitiesDatabase.GetAbility(completedAbilityId);
//
//                         // Проверяем тип доставки напрямую из структуры твоего JSON-конфига
//                         bool isProjectileDelivery = abilityCfg != null 
//                                                     && abilityCfg.delivery != null 
//                                                     && abilityCfg.delivery.type == "projectile";
//
//                         if (isProjectileDelivery)
//                         {
//                             string rawPath = (abilityCfg.delivery != null && abilityCfg.delivery.fx != null)
//                                 ? abilityCfg.delivery.fx.prefab_path
//                                 : "Projectiles/frostbolt_debug";
//
//                             Entity requestEntity = ecb.CreateEntity();
//                             ecb.AddComponent(requestEntity, new ProjectileSpawnRequest
//                             {
//                                 CasterEntity = entity,
//                                 TargetEntity = castTarget,
//                                 AbilityId = cast.AbilityId,
//                                 PrefabPath = rawPath
//                             });
//
//                             Debug.Log($"✉️ [CastSystem]: Приказ ProjectileSpawnRequest выписан с префабом '{rawPath}'.");
//                         }
//                         else
//                         {
//                             // ⚔️ Сюда шёлково залетит Ледяной Доспех! castTarget теперь честно равен игроку
//                             Entity combatEventEntity = ecb.CreateEntity();
//                             ecb.AddComponent(combatEventEntity, new CombatEventRequest
//                             {
//                                 Caster = entity,
//                                 Target = castTarget, 
//                                 AbilityId = cast.AbilityId
//                             });
//
//                             Debug.Log($"⚔️ [CastSystem]: Мгновенный хит способности '{completedAbilityId}'. Отправлен CombatEventRequest на цель {castTarget.Index}.");
//                         }
//                     }
//                     else
//                     {
//                         Debug.LogWarning($"❌ [CastSystem]: Способность '{cast.AbilityId}' прервана — нет легитимной цели!");
//                     }
//                 }
//
//                 // 🌐 ЕДИНЫЙ СЕРВЕРНЫЙ ШЛЮЗ ВИЗУАЛА: отправляем CastEnd ровно ОДИН раз при любом исходе финиша!
//                 if (shouldSendCastEnd && eventBufferSingleton != Entity.Null)
//                 {
//                     ecb.AppendToBuffer(eventBufferSingleton, new PresentationEvent
//                     {
//                         Kind = PresentationEventKind.CastEnd,
//                         Source = entity,
//                         Target = cast.Target,
//                         Param = cast.AbilityId
//                     });
//                 }
//
//                 castRW.ValueRW = cast;
//             }
//         }
//     }
// }
//

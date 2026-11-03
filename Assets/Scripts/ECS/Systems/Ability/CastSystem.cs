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
        // 📥 ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Стартер) - ТВОЙ ЗАВОДСКОЙ ВАРИАНТ
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

                // Твоя девственная заводская валидация из трех аргументов, которая красит кнопки!
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

                        var castData = new CastComponent
                        {
                            IsActive = true,
                            AbilityId = request.ValueRO.AbilityId,
                            CastTime = castTime,
                            Progress = 0f,
                            IsChanneling = isChanneling,
                            Target = request.ValueRO.TargetEntity
                        };

                        if (!em.HasComponent< CastComponent >(caster))
                            ecb.AddComponent(caster, castData);
                        else
                            em.SetComponentData(caster, castData);

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
            
            ecb.AppendToBuffer(eventBuffer, new PresentationEvent {
                Kind = PresentationEventKind.CastStart,
                Source = caster,
                Target = target,
                Param = abilityId
            });
            Debug.Log($"📡 [CastSystem]: В шину презентации улетело событие {PresentationEventKind.CastStart} от юнита {caster.Index}.");
        }

        private void SendCastEndEvent(EntityCommandBuffer ecb, Entity eventBuffer, Entity caster, Entity target, FixedString32Bytes abilityId)
        {
            if (eventBuffer == Entity.Null) return;
            ecb.AppendToBuffer(eventBuffer, new PresentationEvent { Kind = PresentationEventKind.CastEnd, Source = caster, Target = target, Param = abilityId });
        }
    }
}

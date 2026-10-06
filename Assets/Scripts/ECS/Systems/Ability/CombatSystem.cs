using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CombatSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CombatEventRequest>>().WithEntityAccess())
            {
                Entity caster = request.ValueRO.Caster;
                Entity target = request.ValueRO.Target;
                string abilityIdStr = request.ValueRO.AbilityId.ToString();

                if (caster != Entity.Null && target != Entity.Null && em.Exists(caster) && em.Exists(target))
                {
                    var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);

                    if (abilityCfg != null && abilityCfg.effects != null && abilityCfg.effects.Count > 0)
                    {
                        UpdateCombatStates(caster, target, em);

                        // 🚀 ИСТИННЫЙ СИ-КАНOН: Называем переменную КРАСИВО и ПОЛНОСТЬЮ — random!
                        // Чтобы компилятор не слеп, явно скармливаем тип структуры из математики
                        Random random = new Random((uint)(SystemAPI.Time.ElapsedTime * 1000000) + 1);

                        foreach (var effect in abilityCfg.effects)
                        {
                            if (effect == null || string.IsNullOrEmpty(effect.type)) continue;
                            
                            switch (effect.type)
                            {
                                case "direct_damage":
                                    ApplyDirectDamage(target, effect, em, ref random, ecb);
                                    break;

                                case "direct_heal":
                                    ApplyDirectHeal(target, effect, em, ref random);
                                    break;

                                case "apply_aura":
                                    ApplyAura(caster, target, effect, em, ecb);
                                    break;
                            }
                        }
                    }
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        // =========================================================================
        // ПРИВАТНЫЕ ИЗОЛИРОВАННЫЕ МЕТOДЫ ПРИМЕНЕНИЯ ЭФФЕКТОВ
        // =========================================================================

        private static void ApplyDirectDamage(Entity target, AbilityEffectConfig effect, EntityManager em, ref Unity.Mathematics.Random random, EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<HealthComponent>(target)) return;

            // ГВАРД СМЕРТИ: Защита на случай, если тэг уже применился кадром ранее
            if (em.HasComponent<IsDeadTag>(target)) return;

            var health = em.GetComponentData<HealthComponent>(target);
            
            // Защита от избыточного урона: если ХП уже на нуле (но тэг еще долетает в буфере) — выходим
            if (health.Current <= 0f) return;

            float baseDamage = random.NextFloat(effect.min, effect.max);
            if (baseDamage <= 0f && effect.value > 0f) baseDamage = effect.value;

            health.Current = math.max(0f, health.Current - baseDamage);
            em.SetComponentData(target, health);

            Debug.Log($"⚔️ [CombatSystem]: Цель {target} получила {baseDamage:F1} {effect.damage_type} урона! ХП: {health.Current}/{health.Max}");

            // 🪦 WOW-КАНОН СМЕРТИ: Если ХП иссякло — отправляем отложенный тэг смерти!
            if (health.Current <= 0f)
            {
                // 🦾 ПУЛЕНЕПРОБИВАЕМЫЙ ECS-СЛOЙ:
                // Вместо EntityManager вызываем ecb.AddComponent! 
                // Команда шёлково запишется в буфер, и применится сразу на выходе из системы, 
                // полностью ликвидируя краш InvalidOperationException!
                ecb.AddComponent<IsDeadTag>(target);
                
                Debug.Log($"🪦 [CombatSystem]: Сущность {target} скончалась. Команда накат IsDeadTag отправлена в ECB.");
            }
        }


        private static void ApplyDirectHeal(Entity target, AbilityEffectConfig effect, EntityManager em, ref Unity.Mathematics.Random random)
        {
            if (!em.HasComponent<HealthComponent>(target)) return;

            // 🪦 Труп вылечить прямым хилом нельзя!
            if (em.HasComponent<IsDeadTag>(target)) return;

            var health = em.GetComponentData<HealthComponent>(target);

            float baseHeal = random.NextFloat(effect.min, effect.max);
            if (baseHeal <= 0f && effect.value > 0f) baseHeal = effect.value;

            health.Current = math.min(health.Max, health.Current + baseHeal);
            em.SetComponentData(target, health);

            Debug.Log($"💚 [CombatSystem]: Цель {target} исцелена на {baseHeal:F1}! ХП: {health.Current}/{health.Max}");
        }

        private static void ApplyAura(Entity caster, Entity target, AbilityEffectConfig effect, EntityManager em, EntityCommandBuffer ecb)
        {
            if (em.HasComponent<IsDeadTag>(target)) return;

            string auraId = effect.aura_id;
            if (string.IsNullOrEmpty(auraId)) return;

            var auraCfg = AurasDatabase.GetAura(auraId);
            if (auraCfg == null) return;

            if (!em.HasComponent<BuffersLinkComponent>(target)) return;
            var links = em.GetComponentData<BuffersLinkComponent>(target);
            Entity auraContainer = links.AuraFrame;

            if (auraContainer == Entity.Null || !em.Exists(auraContainer)) return;

            var auraBuffer = em.GetBuffer<AuraSlot>(auraContainer);
            
            // 🦾 СИ-ФИКС №2: ТОТАЛЬНЫЙ ГВАРД ПЕРЕИСПОЛЬЗОВАНИЯ ПАМЯТИ ЧАНКОВ!
            // Если буфер контейнера пустой (длина 0, только что заспавнился моб),
            // мы обязаны принудительно набить его стерильными пустыми Си-ячейками на 8 слотов,
            // чтобы стереть любые остатки аур от старых умерших скелетов из этого куска ОЗУ!
            if (auraBuffer.Length == 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    var emptySlot = new AuraSlot { SlotIndex = i };
                    emptySlot.ClearContent(); // Вызываем твое точечное самоочищение!
                    auraBuffer.Add(emptySlot);
                }
            }

            bool isFound = false;
            float auraDuration = effect.value > 0f ? effect.value : (auraCfg.duration > 0f ? auraCfg.duration : 30f);

            // БЛОК 1: Ищем существующую ауру для обновления таймера и стаков
            for (int i = 0; i < auraBuffer.Length; i++)
            {
                var slot = auraBuffer[i];
                if (slot.AuraId == auraId)
                {
                    slot.TimeRemaining = auraDuration;
                    slot.Duration = auraDuration;
                    
                    int maxAllowedStacks = auraCfg.max_stacks > 0 ? auraCfg.max_stacks : 1;
                    slot.Stacks = math.min(maxAllowedStacks, slot.Stacks + 1);
                    slot.TickTimer = 0f;

                    auraBuffer[i] = slot;
                    isFound = true;
                    Debug.Log($"🔄 [CombatSystem]: Аура '{auraId}' обновлена в ОЗУ.");
                    break;
                }
            }

            // БЛОК 2: Если бафф новый — пишем строго в свободную Си-ячейку
            if (!isFound)
            {
                for (int i = 0; i < auraBuffer.Length; i++)
                {
                    var slot = auraBuffer[i];
                    if (slot.IsEmpty)
                    {
                        slot.AuraId = auraId;
                        slot.TimeRemaining = auraDuration;
                        slot.Duration = auraDuration;
                        slot.Stacks = 1;
                        slot.CasterEntity = caster;
                        slot.TargetEntity = target;
                        slot.TickTimer = 0f;

                        auraBuffer[i] = slot;
                        isFound = true;
                        Debug.Log($"🔮 [CombatSystem]: Новая аура '{auraId}' впрыснута в пустой Си-слот {i}.");
                        break;
                    }
                }
            }

            // Наш пуленепробиваемый шлюз презентации для спавна 3D-рун и ивентов
            if (isFound)
            {
                UpdateCombatStates(caster, target, em);

                var bufferQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PresentationEventBufferTag>());
                if (!bufferQuery.IsEmpty)
                {
                    Entity eventBufferSingleton = bufferQuery.GetSingletonEntity();
                    var eventBuffer = em.GetBuffer<PresentationEvent>(eventBufferSingleton);
                    
                    eventBuffer.Add(new PresentationEvent
                    {
                        Kind = PresentationEventKind.AuraApplied,
                        Source = caster,
                        Target = target,
                        Param = auraId
                    });
                }
            }
        }

        private static void UpdateCombatStates(Entity caster, Entity target, EntityManager em)
        {
            // 1. Вводим кастера в режим боя (если у него есть боевой компонент)
            if (em.HasComponent<CombatStateComponent>(caster))
            {
                var casterCombat = em.GetComponentData<CombatStateComponent>(caster);
                casterCombat.IsInCombat = true;
                em.SetComponentData(caster, casterCombat);
            }

            // 2. Вводим цель в режим боя
            if (em.HasComponent<CombatStateComponent>(target))
            {
                var targetCombat = em.GetComponentData<CombatStateComponent>(target);
                targetCombat.IsInCombat = true;

                // 🦾 ААА-ГВАРД СЕЛФ-ТАРГЕТИНГА:
                // Если у цели нет таргета, мы заставляем её повернуться лицом к обидчику (caster)
                // СТРОГО тогда, когда кастер и цель — это РАЗНЫЕ сущности (например, Маг и Скелет)!
                // Если маг баффнул сам себя — этот блок шёлково пролетит мимо, сохранив таргет стерильным!
                if (targetCombat.CurrentTarget == Entity.Null && caster != target)
                {
                    targetCombat.CurrentTarget = caster;
                }

                em.SetComponentData(target, targetCombat);
            }
        }
    }
}


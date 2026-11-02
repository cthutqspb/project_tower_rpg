using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Utils;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CombatSystem : SystemBase
    {
        private EntityQuery _presentationBufferQuery;

        protected override void OnCreate()
        {
            // 🎯 Кэшируем запрос на шину презентации ОДИН РАЗ при создании системы
            _presentationBufferQuery = GetEntityQuery(ComponentType.ReadOnly<PresentationEventBufferTag>());
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            if (!SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
            {
                Debug.LogError("[CombatSystem]: Не найдена шина GameEvent!");
                return;
            }

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

                        Random random = new Random((uint)(SystemAPI.Time.ElapsedTime * 1000000) + 1);

                        foreach (var effect in abilityCfg.effects)
                        {
                            if (effect == null || string.IsNullOrEmpty(effect.type)) continue;
                            
                            switch (effect.type)
                            {
                                case "direct_damage":
                                    ApplyDirectDamage(caster, target, effect, em, ref random, ecb, gameEventsEntity);
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

        private void ApplyDirectDamage(
            Entity caster,
            Entity target,
            AbilityEffectConfig effect,
            EntityManager em,
            ref Unity.Mathematics.Random random,
            EntityCommandBuffer ecb,
            Entity gameEventsEntity)
        {
            float baseDamage = random.NextFloat(effect.min, effect.max);
            if (baseDamage <= 0f && effect.value > 0f) baseDamage = effect.value;

            // 🎯 Берем шину презентации из закэшированного запроса (без создания каждый кадр)
            Entity presentationEntity = Entity.Null;
            if (!_presentationBufferQuery.IsEmpty)
            {
                presentationEntity = _presentationBufferQuery.GetSingletonEntity();
            }

            AbilityUtils.ApplyDamage(caster, target, baseDamage, em, ecb, gameEventsEntity, presentationEntity);
        }

        private static void ApplyDirectHeal(Entity target, AbilityEffectConfig effect, EntityManager em, ref Unity.Mathematics.Random random)
        {
            if (!em.HasComponent<HealthComponent>(target)) return;

            if (em.HasComponent<IsDeadTag>(target)) return;

            var health = em.GetComponentData<HealthComponent>(target);

            float baseHeal = random.NextFloat(effect.min, effect.max);
            if (baseHeal <= 0f && effect.value > 0f) baseHeal = effect.value;

            health.Current = math.min(health.Max, health.Current + baseHeal);
            em.SetComponentData(target, health);

            Debug.Log($"💚 [CombatSystem]: Цель {target} исцелена на {baseHeal:F1}! ХП: {health.Current}/{health.Max}");
        }

        private void ApplyAura(Entity caster, Entity target, AbilityEffectConfig effect, EntityManager em, EntityCommandBuffer ecb)
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
            
            if (auraBuffer.Length == 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    var emptySlot = new AuraSlot { SlotIndex = i };
                    emptySlot.ClearContent();
                    auraBuffer.Add(emptySlot);
                }
            }

            bool isFound = false;
            float auraDuration = effect.value > 0f ? effect.value : (auraCfg.duration > 0f ? auraCfg.duration : 30f);
            
            float baseAbsorb = (auraCfg.modifiers != null) ? auraCfg.modifiers.absorb_damage : 0f;

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

                    if (baseAbsorb > 0f)
                    {
                        slot.CurrentAbsorb = baseAbsorb;
                    }

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

                        if (baseAbsorb > 0f)
                        {
                            slot.CurrentAbsorb = baseAbsorb;
                        }

                        auraBuffer[i] = slot;
                        isFound = true;
                        Debug.Log($"🔮 [CombatSystem]: Новая аура '{auraId}' впрыснута в пустой Си-слот {i}.");
                        break;
                    }
                }
            }

            // Наш пуленепробиваемый шлюз презентации
            if (isFound)
            {
                UpdateCombatStates(caster, target, em);

                if (!_presentationBufferQuery.IsEmpty)
                {
                    Entity eventBufferSingleton = _presentationBufferQuery.GetSingletonEntity();
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
            if (em.HasComponent<CombatStateComponent>(caster))
            {
                var casterCombat = em.GetComponentData<CombatStateComponent>(caster);
                casterCombat.IsInCombat = true;
                em.SetComponentData(caster, casterCombat);
            }

            if (em.HasComponent<CombatStateComponent>(target))
            {
                var targetCombat = em.GetComponentData<CombatStateComponent>(target);
                targetCombat.IsInCombat = true;

                if (targetCombat.CurrentTarget == Entity.Null && caster != target)
                {
                    targetCombat.CurrentTarget = caster;
                }

                em.SetComponentData(target, targetCombat);
            }
        }
    }
}

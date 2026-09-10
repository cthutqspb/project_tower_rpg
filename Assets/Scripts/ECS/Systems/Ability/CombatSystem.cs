using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities;
using Debug = UnityEngine.Debug; // Твой законный алиас для логов, выжигающий using UnityEngine;

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
                                    // Передаем полное имя random по ссылке (ref) в наши методы эффектов
                                    ApplyDirectDamage(target, effect, em, ref random, ecb);
                                    break;

                                case "direct_heal":
                                    ApplyDirectHeal(target, effect, em, ref random);
                                    break;
                            }
                        }
                    }
                }

                // Стираем отработавший пакет-запрос из ОЗУ текущего кадра
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

            Debug.Log($"⚔️ [CombatSystem]: Цель {target} получила {baseDamage:F1} {effect.school} урона! ХП: {health.Current}/{health.Max}");

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

                if (targetCombat.CurrentTarget == Entity.Null)
                {
                    targetCombat.CurrentTarget = caster;
                }

                em.SetComponentData(target, targetCombat);
            }
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Utils
{
    public static class AbilityUtils
    {
        public static float ApplyDamage(
            Entity caster,
            Entity target,
            float baseDamage,
            EntityManager em,
            EntityCommandBuffer ecb,
            Entity gameEventsEntity, // 🎯 Для квестов (EntityKilledEvent)
            Entity presentationEntity = default) // 🎯 Для VFX (AuraRemoved)
        {
            if (baseDamage <= 0f) return 0f;
            if (!em.HasComponent<HealthComponent>(target)) return 0f;
            if (em.HasComponent<IsDeadTag>(target)) return 0f;

            float remainingDamage = baseDamage;

            // 1. Абсорб
            if (em.HasComponent<BuffersLinkComponent>(target))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(target);
                Entity auraContainer = links.AuraFrame;

                if (auraContainer != Entity.Null && em.Exists(auraContainer) && em.HasBuffer<AuraSlot>(auraContainer))
                {
                    var auras = em.GetBuffer<AuraSlot>(auraContainer);

                    for (int i = 0; i < auras.Length; i++)
                    {
                        var aura = auras[i];
                        if (aura.IsEmpty || aura.CurrentAbsorb <= 0f) continue;

                        float absorbed = math.min(remainingDamage, aura.CurrentAbsorb);
                        remainingDamage -= absorbed;
                        aura.CurrentAbsorb -= absorbed;

                        if (aura.CurrentAbsorb <= 0f)
                        {
                            // 🎯 Отправляем визуальное событие (VFX)
                            if (presentationEntity != Entity.Null && em.Exists(presentationEntity))
                            {
                                var eventBuffer = em.GetBuffer<PresentationEvent>(presentationEntity);
                                eventBuffer.Add(new PresentationEvent
                                {
                                    Kind = PresentationEventKind.AuraRemoved,
                                    Target = target,
                                    Param = aura.AuraId
                                });
                            }

                            aura.ClearContent();
                        }

                        auras[i] = aura;
                        if (remainingDamage <= 0f) break;
                    }
                }
            }

            // 2. Наносим урон в HP
            if (remainingDamage > 0f)
            {
                var health = em.GetComponentData<HealthComponent>(target);
                health.Current = math.max(0f, health.Current - remainingDamage);
                em.SetComponentData(target, health);

                Debug.Log($"⚔️ [AbilityUtils]: {target} получил {remainingDamage:F1} урона. HP: {health.Current}/{health.Max}");

                // 3. Смерть
                if (health.Current <= 0f)
                {
                    ecb.AddComponent<IsDeadTag>(target);

                    // 🎯 ОТПРАВЛЯЕМ СОБЫТИЕ СМЕРТИ В ПРЕЗЕНТАЦИЮ
                    if (presentationEntity != Entity.Null && em.Exists(presentationEntity))
                    {
                        var presBuffer = em.GetBuffer<PresentationEvent>(presentationEntity);
                        presBuffer.Add(new PresentationEvent
                        {
                            Kind = PresentationEventKind.Death,
                            Source = target, // В Source передаем самого умершего, чтобы аниматор нашел его модель
                            Target = target
                        });
                    }

                    if (em.HasComponent<UnitComponent>(target) && gameEventsEntity != Entity.Null && em.Exists(gameEventsEntity))
                    {
                        var targetUnit = em.GetComponentData<UnitComponent>(target);
                        var killBuffer = em.GetBuffer<EntityKilledEvent>(gameEventsEntity);
                        killBuffer.Add(new EntityKilledEvent
                        {
                            Source = caster, // TODO: Передать кастера, если нужно
                            Target = target,
                            TargetNameKey = targetUnit.NameKey,
                            TargetRace = targetUnit.Race,
                            TargetFaction = targetUnit.Faction,
                            TargetType = targetUnit.Type,
                            TargetDefaultRank = targetUnit.DefaultRank
                        });
                        
                        Debug.Log($"🪦 [AbilityUtils]: Сущность {target} скончалась. Событие убийства отправлено в GameEvent.");
                    }
                }
            }

            return remainingDamage;
        }
    }
}

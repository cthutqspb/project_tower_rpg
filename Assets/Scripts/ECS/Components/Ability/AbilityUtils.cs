using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Auras;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Utils
{
    public static class AbilityUtils
    {
        /// <summary>
        /// Наносит урон цели, учитывая все активные щиты (абсорб).
        /// Возвращает, сколько урона реально прошло в HP.
        /// </summary>
        public static float ApplyDamage(
            Entity target,
            float baseDamage,
            EntityManager em,
            EntityCommandBuffer ecb,
            Entity presentationEntity = default)
        {
            if (baseDamage <= 0f) return 0f;
            if (!em.HasComponent<HealthComponent>(target)) return 0f;
            if (em.HasComponent<IsDeadTag>(target)) return 0f;

            float remainingDamage = baseDamage;

            // 1. Ищем щиты в аура-контейнере
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
                            aura.ClearContent(); // Щит лопнул

                        auras[i] = aura;

                        if (remainingDamage <= 0f) break;
                    }
                }
            }

            // 2. Наносим остаток в HP
            if (remainingDamage > 0f)
            {
                var health = em.GetComponentData<HealthComponent>(target);
                health.Current = math.max(0f, health.Current - remainingDamage);
                em.SetComponentData(target, health);

                Debug.Log($"⚔️ [AbilityUtils]: {target} получил {remainingDamage:F1} урона. HP: {health.Current}/{health.Max}");

                if (health.Current <= 0f)
                {
                    ecb.AddComponent<IsDeadTag>(target);

                    // Событие убийства (если нужно)
                    if (em.HasComponent<UnitComponent>(target))
                    {
                        // ... твой EntityKilledEvent (если есть шина GameEvent) ...
                    }
                }
            }

            return remainingDamage;
        }
    }
}

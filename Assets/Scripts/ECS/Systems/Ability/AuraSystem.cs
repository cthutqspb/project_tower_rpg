using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Utils; // 🎯 Подключаем AbilityUtils
using ProjectTowerRpg.Core.Auras;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AuraSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var em = EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
            {
                Debug.LogError("[AuraSystem]: Не найдена шина GameEvent!");
                return;
            }

            // 🎯 Кэшируем синглтон визуала ОДИН РАЗ на кадр, а не внутри цикла!
            bool hasPresentationBus = SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var presentationEntity);

            // 🎯 Кэшируем запрос контейнеров аур
            var containerQuery = SystemAPI.QueryBuilder()
                .WithAllRW<AuraSlot>()
                .WithAll<AuraFrameTag>()
                .Build();
            
            if (containerQuery.IsEmpty) return;

            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(World.Unmanaged);
            
            var containers = containerQuery.ToEntityArray(Allocator.Temp);

            for (int c = 0; c < containers.Length; c++)
            {
                Entity containerEntity = containers[c];
                var auraBuffer = em.GetBuffer<AuraSlot>(containerEntity);

                for (int i = 0; i < auraBuffer.Length; i++)
                {
                    var aura = auraBuffer[i];
                    if (aura.IsEmpty) continue;

                    aura.TimeRemaining -= dt;

                    // 🎯 Ищем конфиг по ХЭШУ (ноль аллокаций вместо ToString)
                    var auraCfg = AurasDatabase.GetAura(aura.AuraId.GetHashCode());

                    if (auraCfg != null && auraCfg.effect != null && aura.TimeRemaining > 0f)
                    {
                        aura.TickTimer += dt;
                        float interval = auraCfg.effect.tick_interval > 0f ? auraCfg.effect.tick_interval : 1.0f;

                        if (aura.TickTimer >= interval)
                        {
                            aura.TickTimer -= interval; // Сохраняем дробный остаток

                            // 🎯 СВИЧ ПО ТИПУ ЭФФЕКТА (для будущего расширения)
                            switch (auraCfg.effect.type)
                            {
                                case "periodic_damage":
                                    ApplyPeriodicDamage(aura, auraCfg, em, ecb, gameEventsEntity, hasPresentationBus, presentationEntity);
                                    break;

                                case "periodic_heal":
                                    // TODO: ApplyPeriodicHeal(...)
                                    break;

                                case "passive_modifier":
                                    // Пассивные модификаторы (броня, скорость) обрабатываются в других системах.
                                    // Тут ничего не делаем.
                                    break;
                            }
                        }
                    }

                    // WOW-КАНОН ПРОТУХАНИЯ
                    if (aura.TimeRemaining <= 0f)
                    {
                        // 🎯 ШЛЁМ СОБЫТИЕ ОКОНЧАНИЯ АУРЫ (чтобы удалить VFX щита)
                        if (hasPresentationBus)
                        {
                            ecb.AppendToBuffer(presentationEntity, new PresentationEvent
                            {
                                Kind = PresentationEventKind.AuraEnd,
                                Target = aura.TargetEntity, // На ком висела аура
                                Param = aura.AuraId         // Какая именно аура
                            });
                        }

#if UNITY_EDITOR
                        Debug.Log($"🧹 [AuraSystem]: Срок действия ауры '{aura.AuraId}' на контейнере {containerEntity.Index} истёк. Очищаю Си-ячейку.");
#endif
                        aura.ClearContent(); 
                    }

                    auraBuffer[i] = aura;
                }
            }

            containers.Dispose();
        }

        // =========================================================================
        // ПРИВАТНЫЕ МЕТОДЫ ОБРАБОТКИ ЭФФЕКТОВ
        // =========================================================================
        private static void ApplyPeriodicDamage(
            AuraSlot aura,
            AuraConfig auraCfg,
            EntityManager em,
            EntityCommandBuffer ecb,
            Entity gameEventsEntity,
            bool hasPresentationBus,
            Entity presentationEntity)
        {
            float finalDotDamage = auraCfg.effect.base_damage * aura.Stacks;
            Entity targetUnit = aura.TargetEntity;

            if (targetUnit == Entity.Null || !em.Exists(targetUnit)) return;
            if (!em.HasComponent<HealthComponent>(targetUnit)) return;
            if (em.HasComponent<IsDeadTag>(targetUnit)) return;

            // 🎯 Наносим урон через AbilityUtils (он сам обработает смерть и событие)
            // Передаем presentationEntity, чтобы AbilityUtils мог отправить AuraRemoved при разбитии щита
            AbilityUtils.ApplyDamage(aura.CasterEntity, targetUnit, finalDotDamage, em, ecb, gameEventsEntity, presentationEntity);

            // 🎯 Выстреливаем сигнал Hit через ECB (для визуала)
            if (hasPresentationBus)
            {
                ecb.AppendToBuffer(presentationEntity, new PresentationEvent
                {
                    Kind = PresentationEventKind.Hit,
                    Source = aura.CasterEntity,
                    Target = targetUnit,
                    Param = aura.AuraId
                });
            }

#if UNITY_EDITOR
            Debug.Log($"🔥 [AuraSystem]: Аура '{aura.AuraId}' списала {finalDotDamage} ХП у юнита {targetUnit.Index} (Стаков: {aura.Stacks}).");
#endif
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections; // Добавлено для Allocator
using ProjectTowerRpg.ECS.Components;
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

                            if (auraCfg.effect.type == "periodic_damage")
                            {
                                float finalDotDamage = auraCfg.effect.base_damage * aura.Stacks;
                                Entity targetUnit = aura.TargetEntity;

                                if (targetUnit != Entity.Null && em.HasComponent<HealthComponent>(targetUnit))
                                {
                                    // ГВАРД A: цель уже мертва
                                    if (em.HasComponent<IsDeadTag>(targetUnit))
                                    {
                                        auraBuffer[i] = aura;
                                        continue;
                                    }

                                    var health = em.GetComponentData<HealthComponent>(targetUnit);

                                    // ГВАРД B: HP уже 0
                                    if (health.Current <= 0f)
                                    {
                                        auraBuffer[i] = aura;
                                        continue;
                                    }
                                    
                                    health.Current = math.max(0f, health.Current - finalDotDamage);
                                    em.SetComponentData(targetUnit, health);

                                    // СМЕРТЬ ОТ ДОТ-А
                                    if (health.Current <= 0f)
                                    {
                                        ecb.AddComponent<IsDeadTag>(targetUnit);

                                        if (em.HasComponent<UnitComponent>(targetUnit))
                                        {
                                            var targetUnitComp = em.GetComponentData<UnitComponent>(targetUnit);
                                            // 🎯 Пишем событие через ECB, а не напрямую в буфер
                                            ecb.AppendToBuffer(gameEventsEntity, new EntityKilledEvent
                                            {
                                                Source = aura.CasterEntity,
                                                Target = targetUnit,
                                                TargetNameKey = targetUnitComp.NameKey,
                                                TargetRace = targetUnitComp.Race,
                                                TargetFaction = targetUnitComp.Faction,
                                                TargetType = targetUnitComp.Type,
                                                TargetDefaultRank = targetUnitComp.DefaultRank
                                            });
                                        }

                                        // Снимаем ауру
                                        aura.ClearContent();
                                        auraBuffer[i] = aura;                                       

#if UNITY_EDITOR
                                        Debug.Log($"🪦 [AuraSystem]: Сущность {targetUnit.Index} скончалась от DoT...");
#endif
                                        continue;
                                    }
                                    
                                    // 🎯 Выстреливаем сигнал Hit через ECB, передаем FixedString напрямую
                                    if (hasPresentationBus)
                                    {
                                        ecb.AppendToBuffer(presentationEntity, new PresentationEvent
                                        {
                                            Kind = PresentationEventKind.Hit,
                                            Source = aura.CasterEntity,
                                            Target = targetUnit,
                                            Param = aura.AuraId // Если Param это string, замени на aura.AuraId.ToString()
                                        });
                                    }

#if UNITY_EDITOR
                                    Debug.Log($"🔥 [AuraSystem]: Аура '{aura.AuraId}' списала {finalDotDamage} ХП у юнита {targetUnit.Index} (Стаков: {aura.Stacks}). Осталось ХП: {health.Current}");
#endif
                                }
                            }
                        }
                    }

                    // WOW-КАНОН ПРОТУХАНИЯ
                    if (aura.TimeRemaining <= 0f)
                    {
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
    }
}

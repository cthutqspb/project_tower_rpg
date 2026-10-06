using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Auras;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    // 🦾 СЕРВЕРНЫЙ КАНOН: Дочерняя система боя, покадрово обрабатывающая 
    // unmanaged-ячейки времени, прямое списание здоровья и накат IsDeadTag!
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AuraSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var em = EntityManager;

            // КЭШИРУЕМ МАССИВ СУЩНОСТЕЙ: Выгребаем только контейнеры аур
            var containerQuery = em.CreateEntityQuery(
                ComponentType.ReadWrite< AuraSlot >(), 
                ComponentType.ReadOnly< AuraFrameTag >()
            );
            
            if (containerQuery.IsEmpty) return;

            // Поднимаем ECB для безопасной генерации визуальных сигналов и наложения IsDeadTag из параллельного Си-цикла!
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(World.Unmanaged);
            var containers = containerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            // КЛАССИЧЕСКИЙ СИ-ЦИКЛ: Идем прямо по чанкам памяти контейнеров
            for (int c = 0; c < containers.Length; c++)
            {
                Entity containerEntity = containers[c];
                var auraBuffer = em.GetBuffer< AuraSlot >(containerEntity);

                for (int i = 0; i < auraBuffer.Length; i++)
                {
                    var aura = auraBuffer[i];
                    if (aura.IsEmpty) continue;

                    // Хладнокровно уменьшаем покадровое время баффа в ОЗУ
                    aura.TimeRemaining -= dt;

                    // =========================================================================
                    // 🦾 АППАРАТНЫЙ ОБСЧЁТ ТИКОВ ИЗ УНИВЕРСАЛЬНОГО УЗЛА "EFFECT" (БЕЗ ХАРДКОДА!)
                    // =========================================================================
                    string auraIdStr = aura.AuraId.ToString();
                    var auraCfg = AurasDatabase.GetAura(auraIdStr);

                    // Сверяем стейт по твоему новому полю auraCfg.effect
                    if (auraCfg != null && auraCfg.effect != null && aura.TimeRemaining > 0f)
                    {
                        aura.TickTimer += dt;
                        
                        // Читаем интервал тика прямо из твоего обновленного JSON (например, 1.0 сек)
                        float interval = auraCfg.effect.tick_interval > 0f ? auraCfg.effect.tick_interval : 1.0f;

                        if (aura.TickTimer >= interval)
                        {
                            aura.TickTimer -= interval; // Сохраняем дробный остаток времени кадра

                            // Если тип эффекта ауры — урон во времени (DoT)
                            if (auraCfg.effect.type == "periodic_damage")
                            {
                                // Извлекаем базовый урон и множим на текущие Си-стаки из ячейки
                                float baseDmg = auraCfg.effect.base_damage;
                                float finalDotDamage = baseDmg * aura.Stacks;

                                Entity targetUnit = aura.TargetEntity;

                                // ПРЯМОЕ СЕРВЕРНОЕ СПИСАНИЕ: Модифицируем HealthComponent на месте!
                                if (targetUnit != Entity.Null && em.HasComponent< HealthComponent >(targetUnit))
                                {
                                    var health = em.GetComponentData< HealthComponent >(targetUnit);
                                    
                                    // Списываем урон поджога из здоровья (твое float-поле)
                                    health.Current = math.max(0f, health.Current - finalDotDamage);
                                    
                                    em.SetComponentData(targetUnit, health);

                                    // 🦾 СИ-ФИКС №1: НАМЕРТВО ФИКСИРУЕМ СМЕРТЬ ОТ ДОТ-А!
                                    // Если урон от тика 'ignite' опустил ХП до нуля или ниже — 
                                    // мы обязаны немедленно выписать команду на навешивание IsDeadTag через ECB!
                                    if (health.Current <= 0f)
                                    {
                                        ecb.AddComponent< IsDeadTag >(targetUnit);
                                        Debug.Log($"🪦 [AuraSystem]: Сущность {targetUnit.Index} скончалась от DoT-эффекта '{auraIdStr}'. Команда накат IsDeadTag отправлена в ECB.");
                                    }

                                    // 📡 ШЛЮЗ ПРЕЗЕНТАЦИИ: Выстреливаем слепой сигнал Hit в шину визуала!
                                    // При каждом тике пламени над головой скелета будет сочно вспыхивать и дрожать наша багровая руна
                                    var bufferQuery = em.CreateEntityQuery(ComponentType.ReadOnly< PresentationEventBufferTag >());
                                    if (!bufferQuery.IsEmpty)
                                    {
                                        Entity eventBufferSingleton = bufferQuery.GetSingletonEntity();
                                        var eventBuffer = em.GetBuffer< PresentationEvent >(eventBufferSingleton);
                                        
                                        eventBuffer.Add(new PresentationEvent
                                        {
                                            Kind = PresentationEventKind.Hit,
                                            Source = aura.CasterEntity,
                                            Target = targetUnit,
                                            Param = auraIdStr // Передаем "ignite" для багрового тинта вспышки
                                        });
                                    }

                                    Debug.Log($"🔥 [AuraSystem]: Аура '{auraIdStr}' списала {finalDotDamage} ХП у юнита {targetUnit.Index} (Стаков: {aura.Stacks}). Осталось ХП: {health.Current}");
                                }
                            }
                        }
                    }

                    // WOW-КАНОН ПРOТУХАНИЯ: Время баффа истекло — аннигилируем Си-байты
                    if (aura.TimeRemaining <= 0f)
                    {
                        Debug.Log($"🧹 [AuraSystem]: Срок действия ауры '{aura.AuraId}' на контейнере {containerEntity.Index} истёк. Очищаю Си-ячейку.");
                        aura.ClearContent(); 
                    }

                    // Перезаписываем измененную структуру напрямую в ОЗУ чанка кадра
                    auraBuffer[i] = aura;
                }
            }

            containers.Dispose();
        }
    }
}


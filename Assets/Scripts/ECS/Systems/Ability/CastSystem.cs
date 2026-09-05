using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities; // Твой AbilityConfig и реестр заклинаний

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CastSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            // Безопасный отложенный буфер для создания структурных изменений в конце фазы
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            // =========================================================================
            // ПОТОК 1: ПРИЕМ И ВАЛИДАЦИЯ ЗАПРОСОВ (Серверный шлюз)
            // =========================================================================
            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CastRequest>>().WithEntityAccess())
            {
                Entity casterEntity = request.ValueRO.Caster;

                // Гвард: Сущность должна физически существовать в памяти чанков
                if (casterEntity != Entity.Null && em.Exists(casterEntity))
                {
                    // 🛡️ WoW-ГВАРД: Проверяем, не занята ли туша другим активным кастом прямо сейчас
                    bool isAlreadyCasting = em.HasComponent<CastComponent>(casterEntity) && em.GetComponentData<CastComponent>(casterEntity).IsActive;

                    if (!isAlreadyCasting)
                    {
                        // Вытаскиваем эталонные ТТХ абилки из твоей базы данных способностей!
                        var abilityCfg = AbilitiesDatabase.GetAbility(request.ValueRO.AbilityId.ToString());
                        
                        // Зряче лезем по цепочке в отмытые параметры твоего AbilityConfig
                        float castTime = abilityCfg != null && abilityCfg.parameters != null 
                            ? abilityCfg.parameters.cast_time 
                            : 1.7f; // Наш фоллбэк для тестов

                        bool isChanneling = abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.is_channeling;

                        // Если на существе еще нет CastComponent (первый каст в игре) — добавляем через ECB, иначе пишем поверх
                        if (!em.HasComponent<CastComponent>(casterEntity))
                        {
                            ecb.AddComponent(casterEntity, new CastComponent
                            {
                                IsActive = true,
                                AbilityId = request.ValueRO.AbilityId,
                                CastTime = castTime,
                                Progress = 0f,
                                IsChanneling = isChanneling
                            });
                        }
                        else
                        {
                            em.SetComponentData(casterEntity, new CastComponent
                            {
                                IsActive = true,
                                AbilityId = request.ValueRO.AbilityId,
                                CastTime = castTime,
                                Progress = 0f,
                                IsChanneling = isChanneling
                            });
                        }
                    }
                }

                // Стираем отработавший пакет-запрос из ОЗУ текущего кадра
                ecb.DestroyEntity(requestEntity);
            }

            // =========================================================================
            // ПОТОК 2: ПОКАДРОВЫЙ ТИК ТАЙМЕРОВ КАСTА (Всеядный Си-цикл симуляции)
            // =========================================================================
            foreach (var (castRW, entity) in SystemAPI.Query<RefRW<CastComponent>>().WithEntityAccess())
            {
                if (!castRW.ValueRO.IsActive) continue;

                // 🦾 СЕМАНТИКА: Извлекаем плоскую структуру данных под её честное, чистое имя!
                var cast = castRW.ValueRW;

                // 🦾 WoW-ГВАРД ДВИЖЕНИЯ: Если это НЕ потоковое заклинание на ходу (IsChanneling) — проверяем перемещение!
                if (!cast.IsChanneling && SystemAPI.HasComponent<MovementComponent>(entity))
                {
                    var move = SystemAPI.GetComponent<MovementComponent>(entity);

                    // Идеальная unmanaged-математика: проверяем вектор направления float3 и скалярную скорость!
                    bool isMoving = math.lengthsq(move.Direction) > 0.001f && move.CurrentSpeed > 0.001f; 

                    if (isMoving)
                    {
                        cast.IsActive = false; // Намертво обрываем каст в памяти сервера!
                        cast.Progress = 0f;
                        castRW.ValueRW = cast; // Синхронизируем ОЗУ чанка кадра через RW-указатель

                        Debug.Log($"❌ [CastSystem]: Каст заклинания '{cast.AbilityId}' ПРЕРВАН из-за движения сущности {entity}!");
                        continue; // Каст угас, пулей прыгаем на следующую Entity в чанке
                    }
                }

                cast.Progress += SystemAPI.Time.DeltaTime; // Накапливаем нативные Си-секунды!

                // Проверка успешного завершения заклинания
                if (cast.Progress >= cast.CastTime)
                {
                    cast.IsActive = false; // Каст успешно угас в памяти!
                    Debug.Log($"🔥 [CastSystem]: Каст заклинания '{cast.AbilityId}' УСПЕШНО ЗАВЕРШЕН у сущности {entity}!");
                }

                castRW.ValueRW = cast; // Запекаем обновленные секунды обратно в ОЗУ чанка
            }
        }
    }
}


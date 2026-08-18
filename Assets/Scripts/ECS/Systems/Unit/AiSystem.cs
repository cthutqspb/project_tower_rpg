using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using UnityEngine;

namespace ProjectTowerRpg.ECS.Systems
{
    // Система тикает в главном симуляционном цикле DOTS
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AiSystem : SystemBase
    {
        private Unity.Mathematics.Random _random;

        protected override void OnCreate()
        {
            // Сид для генерации случайных чисел (каноничный, как в твоем unit_ai.lua)
            _random = new Unity.Mathematics.Random(98765);
        }

        protected override void OnUpdate()
        {
            // Берем текущее системное Си-время рантайма и дельту кадра
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;

            // 🚀 СОВРЕМЕННЫЙ DOTS-КОНВЕЙЕР ИИ (Чистый, быстрый и без ошибок компиляции!):
            // Перебираем твои новые компоненты стейта, твой родной MovementComponent
            // и стандартный LocalTransform через нативный SystemAPI.Query
             foreach (var (ai, move, combat, transform) in 
                     SystemAPI.Query<RefRW<AiComponent>, RefRW<MovementComponent>, RefRO<CombatStateComponent>, RefRW<LocalTransform>>())
            {
                // 🛡️ WOW-КАНОН ОПТИМИЗАЦИИ (Твой оригинальный Lua-гвард):
                // Если этот юнит не из фабрики (например, игрок или редакторный призрак) —
                // мы мгновенно прерываем апдейт. Ему запрещено покадрово думать и патрулировать!
                if (!ai.ValueRO.IsFromFactory) continue;

                // 🛡️ ПУЛЕНЕПРОБИВАЕМЫЙ ГВАРД СМЕРТИ (Твой оригинальный Lua-контур):
                if (combat.ValueRO.IsDead)
                {
                    move.ValueRW.direction = float3.zero;
                    continue; // В цикле foreach вместо return пишем continue, чтобы идти к следующему мобу!
                }

                // // TODO: WoW-Канон Боевой фазы на будущее (Utility AI, CHASE / ATTACK, Кайтинг магов)
                // if (combat.ValueRO.IsInCombat) { continue; }

                // ================================================================
                // ФАЗА ПАССИВНОГО МИРНОГО ПОКОЯ (Твой unit_ai.lua один в один)
                // ================================================================
                
                // 🛑 СТEЙТ 1: IDLE (Время раздумий на точке)
                if (!ai.ValueRO.HasTarget && currentTime >= ai.ValueRO.NextActionTime)
                {
                    // Выбираем случайную точку патруля в радиусе ±70 единиц от дома (StartPoint)
                    float2 offset = _random.NextFloat2Direction() * _random.NextFloat(20f, ai.ValueRO.PatrolRadius);
                    ai.ValueRW.CurrentTarget = ai.ValueRO.StartPoint + new float3(offset.x, 0f, offset.y);
                    ai.ValueRW.HasTarget = true;
                    ai.ValueRW.IsPatrolling = true; // Взводим флаг патруля для снижения скорости
                }

                // 🏃‍♂️ СТEЙТ 2: PATROL (Покадровый расчет вектора и сдвиг)
                if (ai.ValueRO.HasTarget)
                {
                    // 1. Считаем сырой вектор до цели патруля
                    float3 vectorToTarget = ai.ValueRO.CurrentTarget - transform.ValueRO.Position;

                    // =========================================================================
                    // 🚀 КАНОН ВЕРТИКАЛЬНОСТИ ИЗ КИШЕК DEFOLD (ИСПРАВЛЕНО НАМЕРТВО):
                    // Мы КАТЕГОРИЧЕСКИ обнуляем ось Y ДО нормализации вектора!
                    // Математический калькулятор ИИ выдаст абсолютно плоский вектор движения по земле.
                    // =========================================================================
                    vectorToTarget.y = 0f; 

                    float distance = math.length(vectorToTarget);

                    // Успешно пришли в точку патрулирования? (Допуск 0.4 метра)
                    if (distance < 0.4f)
                    {
                        move.ValueRW.direction = float3.zero; // Обнуляем вектор в твоем компоненте
                        ai.ValueRW.HasTarget = false;
                        ai.ValueRW.IsPatrolling = false;
                        
                        float randomDelay = _random.NextInt(10, 31) / 10f;
                        ai.ValueRW.NextActionTime = currentTime + randomDelay;
                        continue;
                    }

                    // 2. Теперь нормализуем АБСОЛЮТНО ПЛОСКИЙ вектор и пушим в твой MovementComponent!
                    // move.direction.y гарантированно станет равен СТРОГО 0.0000f!
                    move.ValueRW.direction = math.normalize(vectorToTarget);

                    // Задаем скорость из боевого паспорта существа (2.0 м/с или 1.0 м/с в патруле)
                    float workingSpeed = combat.ValueRO.CurrentSpeed;
                    if (ai.ValueRO.IsPatrolling)
                    {
                        workingSpeed = workingSpeed * 0.5f; 
                    }
                    move.ValueRW.speed = workingSpeed;

                    // ❌ СДВИГ КООРДИНАТ (transform.Position += ...) ОТСЮДА УДАЛЕН НАВСЕГДА!
                    // Твоя родная монолитная MovementSystem сама шёлково передвинет тушу в ОЗУ.
                }
            }
        }
   }
}


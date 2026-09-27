using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units; // Возвращаем доступ к UnitsDatabase для строкового поиска
using ProjectTowerRpg.Core.Abilities;

namespace ProjectTowerRpg.ECS.Systems
{
    // Система тикает в главном симуляционном цикле DOTS
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AiSystem : SystemBase
    {
        private Unity.Mathematics.Random _random;
        
        private static bool TrySelectBestAbility(
            DynamicBuffer<ActiveCooldownElement> cooldowns, 
            ResourceComponent sourceResource, 
            UnitConfig dbCfg, 
            float distanceToTarget, 
            out string bestAbilityId, 
            out float bestAbilityRange)
        {
            bestAbilityId = "melee_attack";
            bestAbilityRange = 1.5f;

            if (dbCfg.abilities == null || dbCfg.abilities.Count == 0)
                return true;

            float highestScore = -999999f;
            bool foundValid = false;

            for (int i = 0; i < dbCfg.abilities.Count; i++)
            {
                string abilityId = dbCfg.abilities[i];
                var cfg = AbilitiesDatabase.GetAbility(abilityId);
                if (cfg == null) continue;

                bool isUsable = true;

                // Проверка ресурсов
                if (cfg.cost != null && cfg.cost.value > 0)
                {
                    if (sourceResource.Current < cfg.cost.value)
                        isUsable = false;
                }

                // Проверка дистанции
                if (isUsable && cfg.parameters != null)
                {
                    if (distanceToTarget > cfg.parameters.range)
                        isUsable = false;
                }

                if (!isUsable) continue;

                // Скоринг по тегам
                float currentScore = 1.0f;

                if (cfg.identity != null && cfg.identity.tags != null)
                {
                    for (int t = 0; t < cfg.identity.tags.Count; t++)
                    {
                        string tagName = cfg.identity.tags[t];

                        if (dbCfg.ai.tag_weights != null 
                            && dbCfg.ai.tag_weights.TryGetValue(tagName, out float multiplier))
                        {
                            currentScore *= multiplier;
                        }
                    }
                }

                // Выбор лучшего
                if (currentScore > highestScore)
                {
                    highestScore = currentScore;
                    bestAbilityId = abilityId;
                    bestAbilityRange = cfg.parameters != null ? cfg.parameters.range : 1.5f;
                    foundValid = true;
                }
            }

            return foundValid;
        }

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

            // Обновляем состояние рандома от времени кадра, чтобы сид не зацикливался внутри partial-системы
            _random = new Unity.Mathematics.Random((uint)(currentTime * 10000) + 1);

            // 🚀 СОВРЕМЕННЫЙ DOTS-КОНВЕЙЕР ИИ (Чистый, быстрый и без ошибок компиляции!):
            // Перебираем твои новые компоненты стейта, твой родной MovementComponent
            // и стандартный LocalTransform через нативный SystemAPI.Query
            foreach (var (ai, move, combat, transform, unitData) in 
                     SystemAPI.Query<RefRW<AiComponent>, RefRW<MovementComponent>, RefRO<CombatStateComponent>, RefRW<LocalTransform>, RefRO<UnitComponent>>().WithNone<IsDeadTag>())
            {                // 🛡️ WOW-КАНОН ОПТИМИЗАЦИИ (Твой оригинальный Lua-гвард):
                // Если этот юнит не из фабрики (например, игрок или редакторный призрак) —
                // мы мгновенно прерываем апдейт. Ему запрещено покадрово думать и патрулировать!
                if (!ai.ValueRO.IsFromFactory) continue;

                // // TODO: WoW-Канон Боевой фазы на будущее (Utility AI, CHASE / ATTACK, Кайтинг магов)
                // if (combat.ValueRO.IsInCombat) { continue; }

                // ================================================================
                // СТАБИЛЬНЫЙ СТРОКОВЫЙ ПОИСК КОНФИГА ИЗ ТВОЕЙ БАЗЫ ДАННЫХ
                // ================================================================
                string uIdStr = unitData.ValueRO.UnitId.ToString().ToLower().Trim();
                var dbCfg = UnitsDatabase.GetUnit(uIdStr);
                if (dbCfg == null) continue;

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
                        move.ValueRW.Direction = float3.zero; // Обнуляем вектор в твоем компоненте
                        ai.ValueRW.HasTarget = false;
                        ai.ValueRW.IsPatrolling = false;
                        
                        float randomDelay = _random.NextInt(10, 31) / 10f;
                        ai.ValueRW.NextActionTime = currentTime + randomDelay;
                        continue;
                    }

                    // 2. Теперь нормализуем АБСОЛЮТНО ПЛОСКИЙ вектор и пушим в твой MovementComponent!
                    // move.direction.y гарантированно станет равен СТРОГО 0.0000f!
                    move.ValueRW.Direction = math.normalize(vectorToTarget);

                    // 🦾 ИСПРАВЛЕНИЕ СКОРОСТИ ДЛЯ ПАТРУЛЯ: 
                    // Задаем скорость напрямую из стабильного JSON-конфига юнита!
                    float workingSpeed = dbCfg.parameters.base_speed;
                    if (ai.ValueRO.IsPatrolling)
                    {
                        workingSpeed = workingSpeed * 0.5f; 
                    }
                    move.ValueRW.CurrentSpeed = workingSpeed;

                    // ❌ СДВИГ КООРДИНАТ (transform.Position += ...) ОТСЮДА УДАЛЕН НАВСЕГДА!
                    // Твоя родная монолитная MovementSystem сама шёлково передвинет тушу в ОЗУ.
                }
            }
        }
   }
}


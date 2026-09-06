using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GlobalCooldownSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 🦾 ТОТАЛЬНАЯ ЛОКАЛЬНОСТЬ: Прокачиваем таймеры внутри боевого паспорта юнитов!
            // Burst-компилятор соберет этот плоский цикл в идеальные векторные инструкции.
            foreach (var combatRW in SystemAPI.Query<RefRW<CombatStateComponent>>())
            {
                var combat = combatRW.ValueRW;

                if (combat.GcdRemaining > 0f)
                {
                    // Вычитаем Си-секунды текущего кадра симуляции мира
                    combat.GcdRemaining -= SystemAPI.Time.DeltaTime;

                    // Страхуем ОЗУ от ухода в отрицательные числа
                    if (combat.GcdRemaining <= 0f)
                    {
                        combat.GcdRemaining = 0f;
                        combat.GcdDuration = 0f; // Сбрасываем эталон, ГКД полностью остыло!
                    }

                    combatRW.ValueRW = combat; // Запекаем стейт обратно в чанк
                }
            }
        }
    }
}


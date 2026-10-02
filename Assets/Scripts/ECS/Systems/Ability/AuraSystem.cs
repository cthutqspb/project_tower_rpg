using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;
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

            // 🦾 1. КЭШИРУЕМ МАССИВ СУЩНОСТЕЙ: Выгребаем только контейнеры аур
            var containerQuery = em.CreateEntityQuery(
                ComponentType.ReadWrite< AuraSlot >(), 
                ComponentType.ReadOnly< AuraFrameTag >()
            );
            
            if (containerQuery.IsEmpty) return;

            // Собираем сущности во временный быстрый unmanaged-массив
            var containers = containerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            // 🦾 2. КЛАССИЧЕСКИЙ СИ-ЦИКЛ: Идем прямо по чанкам памяти контейнеров
            for (int c = 0; c < containers.Length; c++)
            {
                Entity containerEntity = containers[c];
                
                // Достаем легитимную, открытую на запись Си-ссылку на буфер аур!
                var auraBuffer = em.GetBuffer< AuraSlot >(containerEntity);

                for (int i = 0; i < auraBuffer.Length; i++)
                {
                    var aura = auraBuffer[i];

                    if (aura.IsEmpty) continue;

                    // Уменьшаем покадровое время баффа
                    aura.TimeRemaining = math.max(0f, aura.TimeRemaining - dt);

                    // 🪦 WOW-КАНОН: Время баффа истекло — аннигилируем Си-байты
                    if (aura.TimeRemaining <= 0f)
                    {
                        Debug.Log($"🧹 [AuraSystem]: Срок действия ауры '{aura.AbilityId}' на контейнере {containerEntity.Index} истёк. Очищаю Си-ячейку.");
                        
                        aura.AbilityId = "";
                        aura.Stacks = 0;
                        aura.Duration = 0f;
                        aura.CasterEntity = Entity.Null;
                    }

                    // ТЕПЕРЬ ЭТО РАБОТАЕТ БЕЗБАЖНО: Перезаписываем структуру напрямую в ОЗУ чанка!
                    auraBuffer[i] = aura;
                }
            }

            // Обязательно освобождаем unmanaged-память массива в конце кадра
            containers.Dispose();
        }
    }
}


using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Reducers
{
    // 🦾 СТEРИЛЬНЫЙ МОДУЛЬ ИГРОКА: Избавлен от конфликтов имён, работает только с ОЗУ игрока!
    public static class PlayerReducer
    {
        public static void ActionBarAssign(EntityManager em, ActionCommand cmd)
        {
            if (!em.HasBuffer<ActionBarSlot>(cmd.TargetEntity)) return;
            var barBuffer = em.GetBuffer<ActionBarSlot>(cmd.TargetEntity);

            // =========================================================================
            // 🦾 КЕЙС 0: ДРОП В ПУСТОЙ МИР (Удаление ярлыка с панели в молоко)
            // =========================================================================
            if (cmd.TargetSlot == -1)
            {
                if (em.HasComponent<PlayerTag>(cmd.SourceEntity) && cmd.SourceSlot >= 0 && cmd.SourceSlot < barBuffer.Length)
                {
                    var sourceSlot = barBuffer[cmd.SourceSlot];
                    sourceSlot.AbilityId = "";
                    barBuffer[cmd.SourceSlot] = sourceSlot;
                }
                return;
            }

            // =========================================================================
            // 🦾 КЕЙС 1: АТОМАРНАЯ ЗАПИСЬ ЯРЛЫКА В ЦЕЛЬ
            // =========================================================================
            var targetSlot = barBuffer[cmd.TargetSlot];
            var tmpAbilityId = targetSlot.AbilityId;

            targetSlot.AbilityId = cmd.ItemId;
            barBuffer[cmd.TargetSlot] = targetSlot; 

            // =========================================================================
            // 🦾 КЕЙС 2: РОКИРОВКА (СВОП) КНОПОК ПАНЕЛИ
            // =========================================================================
            if (em.HasComponent<PlayerTag>(cmd.SourceEntity))
            {
                var sourceSlot = barBuffer[cmd.SourceSlot]; 

                if (!tmpAbilityId.IsEmpty)
                {
                    sourceSlot.AbilityId = tmpAbilityId;
                }
                else
                {
                    sourceSlot.AbilityId = "";
                }

                barBuffer[cmd.SourceSlot] = sourceSlot;
                Debug.Log($"[PlayerReducer]: Перенос ярлыков внутри панелей успешно завершен.");
            }
        }

        public static void AuraDisable(EntityManager em, ActionCommand cmd)
        {
            // 🦾 ГВАРД БЕЗОПАСНОСТИ: Проверяем, существует ли вообще контейнер аур
            // и есть ли на нём легитимный DynamicBuffer
            if (cmd.SourceEntity == Entity.Null || !em.Exists(cmd.SourceEntity)) return;
            if (!em.HasBuffer<AuraSlot>(cmd.SourceEntity)) return;

            var auraBuffer = em.GetBuffer<AuraSlot>(cmd.SourceEntity);
            int targetSlotIndex = cmd.SourceSlot;

            // Проверяем границы массива, чтобы не словить IndexOutOfRangeException
            if (targetSlotIndex >= 0 && targetSlotIndex < auraBuffer.Length)
            {
                var auraSlot = auraBuffer[targetSlotIndex];

                // Валидируем Си-паспорт: стираем ауру только если ID в ячейке совпадает с тем, по которому кликнули
                if (auraSlot.AuraId == cmd.ItemId)
                {
                    Debug.Log($"🧹 [PlayerReducer]: Аннигилирую бафф '{cmd.ItemId}' из ячейки #{targetSlotIndex} контейнера {cmd.SourceEntity.Index} по приказу игрока (ПКМ).");

                    // Полностью сбрасываем структуру в девственный ноль (IsEmpty)
                    auraSlot.AuraId = "";
                    auraSlot.Stacks = 0;
                    auraSlot.TimeRemaining = 0f;
                    auraSlot.Duration = 0f;
                    auraSlot.CasterEntity = Entity.Null;

                    // Перезаписываем очищенные Си-байты обратно в ОЗУ чанка
                    auraBuffer[targetSlotIndex] = auraSlot;
                }
            }
        }  
    }
}


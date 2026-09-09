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
                    var sourceSlotData = barBuffer[cmd.SourceSlot];
                    sourceSlotData.AbilityId = "";
                    barBuffer[cmd.SourceSlot] = sourceSlotData;
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
    }
}


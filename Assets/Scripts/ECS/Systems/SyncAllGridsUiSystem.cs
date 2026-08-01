using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SyncAllGridsUiSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Используем каноничный SystemAPI.Query вместо старого Entities.ForEach
            // Фильтр изменений .WithChangeFilter<SlotData>() теперь пишется в самом запросе
            foreach (var (slots, entity) in 
                     SystemAPI.Query<DynamicBuffer<SlotData>>()
                              .WithChangeFilter<SlotData>()
                              .WithEntityAccess())
            {
                // Ищем чистый UI-компонент StaticGrid в твоем реестре по Entity ID этой сетки
                var uiGrid = EntityRegistry.GetGrid(entity);
                if (uiGrid == null) continue;

                // Переливаем измененные данные в чистый UI
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    uiGrid.UpdateSlot(
                        slot.SlotIndex,
                        slot.DataId.ToString(),
                        slot.DataType.ToString(),
                        slot.Amount,
                        slot.ContainerType.ToString()
                    );
                }
            }
        }
    }
}



using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ActionBarValidationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 🦾 ММО-КАНОН: Ищем сущность, которой мы сейчас физически управляем в ОЗУ чанков!
            if (!SystemAPI.TryGetSingletonEntity<ControlledByPlayerTag>(out var controlledEntity)) return;

            // Забираем безопасный Read-Only Lookup, чтобы намертво заблокировать ложные триггеры статов
            var barLookup = SystemAPI.GetBufferLookup<ActionBarSlot>(true);
            if (!barLookup.HasBuffer(controlledEntity)) return;

            var barSlots = barLookup[controlledEntity];
            
            // Получаем все сетки экшенбара, привязанные к управляемому существу
            var receivers = UIRegistry.GetReceivers(controlledEntity);
            if (receivers == null) return;

            foreach (var receiver in receivers)
            {
                if (receiver is IEcsUiBufferReceiver<ActionBarSlot> barUi)
                {
                    // 🚀 ПОКАДРОВЫЙ ПУШ: Метод вызывается каждую миллисекунду кадра,
                    // заставляя логи спамиться, а хоткеи — сочно алеть на лету!
                    barUi.UpdateFromBuffer(barSlots, true);
                }
            }
        }
    }
}


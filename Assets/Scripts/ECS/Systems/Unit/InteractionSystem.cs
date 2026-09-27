using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.Core.Utils;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class InteractionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (buffer, entity) in 
                     SystemAPI.Query<DynamicBuffer<InteractionEntry>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
            {
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var interaction = buffer[i];
                    if (interaction.TargetEntity == Entity.Null) continue;

                    float distance = PositionUtils.GetDistance(entity, interaction.TargetEntity, EntityManager);

                    if (distance > interaction.MaxDistance)
                    {                        
                        // 📦 1. СТАТИЧНЫЙ КОНТЕЙНЕР (Сундук / Мешок)
                        if (SystemAPI.HasComponent<ItemComponent>(interaction.TargetEntity))
                        {
                            UIEvents.TriggerCloseWindow(WindowType.Container, interaction.TargetEntity);
                        }
                        
                        // 🧑 2. СУЩЕСТВО (Труп монстра или NPC)
                        if (SystemAPI.HasComponent<UnitComponent>(interaction.TargetEntity))
                        {
                            // 🦾 ГВАРД СМЕРТИ: Если юнит мертв, значит мы грабили его карманы как сундук!
                            // Закрываем универсальное окно добычи ContainerWindow.
                            if (SystemAPI.HasComponent<IsDeadTag>(interaction.TargetEntity))
                            {
                                UIEvents.TriggerCloseWindow(WindowType.Container, interaction.TargetEntity);
                            }
                            else
                            {
                                // В БУДУЩЕМ: Если живой — закрываем окно диалога / торговли
                                // UIEvents.TriggerCloseNpcDialog(interaction.TargetEntity);
                            }
                        }

                        // 🚪 ОБЪЕКТ (Object) — закомментировано, пока нет реализации
                        // else if (SystemAPI.HasComponent<ObjectComponent>(interaction.TargetEntity))
                        // {
                        //     UIEvents.TriggerCloseObjectWindow(interaction.TargetEntity);
                        // }

                        buffer.RemoveAt(i);
                    }
                }
            }
        }
    }
}

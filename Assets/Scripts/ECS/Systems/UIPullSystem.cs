using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UIPullSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var slotLookup = SystemAPI.GetBufferLookup<SlotData>(true);

            foreach (var entity in UIRegistry.GetActiveEntities())
            {
                if (!slotLookup.HasBuffer(entity)) continue;
                if (!slotLookup.DidChange(entity, LastSystemVersion)) continue;

                var slots = slotLookup[entity];
                var receivers = UIRegistry.GetReceivers(entity);
                if (receivers == null) continue;

                foreach (var receiver in receivers)
                {
                    if (receiver is IEcsUiBufferReceiver<SlotData> ui)
                    {
                        ui.UpdateFromBuffer(slots);
                    }
                }
            }
        }
    }
}

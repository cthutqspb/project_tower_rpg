using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(QuestSystem))]
[UpdateBefore(typeof(GameEventCleanupSystem))]
    public partial class QuestRewardPopupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity)) return;

            var em = EntityManager;
            var buf = em.GetBuffer<QuestReadyToClaimEvent>(gameEventsEntity);

            if (buf.Length == 0) return;

            for (int i = 0; i < buf.Length; i++)
            {
                var evt = buf[i];
                Debug.Log($"[QuestRewardPopupSystem] Открываю попап наград: '{evt.QuestId}' для {evt.Unit.Index}");
                UIEvents.TriggerOpenWindow(WindowType.QuestEnd, evt.Unit, evt.QuestId.ToString());
            }
        }
    }
}

using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class GameEventCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<GameEvent>(out var gameEventsEntity))
                return;

            var em = EntityManager;
            em.GetBuffer<EntityKilledEvent>(gameEventsEntity).Clear();
            em.GetBuffer<ItemUsedEvent>(gameEventsEntity).Clear();
            em.GetBuffer<DiscoverLocationEvent>(gameEventsEntity).Clear();
            em.GetBuffer<DialogueCompletedEvent>(gameEventsEntity).Clear();
            em.GetBuffer<QuestReadyToClaimEvent>(gameEventsEntity).Clear();
        }
    }
}

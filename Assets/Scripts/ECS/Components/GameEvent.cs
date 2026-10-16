using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Маркер-носитель: на этой сущности лежат все буферы геймплейных событий.
    public struct GameEvent : IComponentData { }

    public struct EntityKilledEvent : IBufferElementData
    {
        public Entity Killer;
        public Entity Victim;
        public FixedString64Bytes VictimNameKey;
        public FixedString32Bytes VictimRace;
        public FixedString32Bytes VictimFaction;
        public FixedString32Bytes VictimType;
        public FixedString32Bytes VictimDefaultRank;
    }

    public struct ItemUsedEvent : IBufferElementData
    {
        public Entity User;
        public FixedString64Bytes ItemId;
    }

    public struct DiscoverLocationEvent : IBufferElementData
    {
        public Entity Discoverer;
        public FixedString64Bytes ZoneId;
    }

    public struct DialogueCompletedEvent : IBufferElementData
    {
        public Entity Listener;
        public Entity Speaker;
        public FixedString64Bytes SpeakerNameKey;
        public FixedString64Bytes DialogueId;
    }
}

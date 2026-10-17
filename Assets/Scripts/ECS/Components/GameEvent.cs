using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Маркер-носитель: на этой сущности лежат все буферы геймплейных событий.
    public struct GameEvent : IComponentData { }

    public struct EntityKilledEvent : IBufferElementData
    {
        public Entity Source;                       // кто убил
        public Entity Target;                       // кого убили
        public FixedString64Bytes TargetNameKey;
        public FixedString32Bytes TargetRace;
        public FixedString32Bytes TargetFaction;
        public FixedString32Bytes TargetType;
        public FixedString32Bytes TargetDefaultRank;
    }

    public struct ItemUsedEvent : IBufferElementData
    {
        public Entity Source;                       // кто использовал
        public FixedString64Bytes ItemId;
    }

    public struct DiscoverLocationEvent : IBufferElementData
    {
        public Entity Source;                       // кто обнаружил
        public FixedString64Bytes ZoneId;
    }

    public struct DialogueCompletedEvent : IBufferElementData
    {
        public Entity Source;                       // игрок (инициатор)
        public Entity Target;                       // NPC (с кем говорили)
        public FixedString64Bytes TargetNameKey;
        public FixedString64Bytes DialogueId;
    }

    public struct QuestReadyToClaimEvent : IBufferElementData
    {
        public Entity Unit;                       // кому сдавать
        public FixedString64Bytes QuestId;        // какой квест
    }
}

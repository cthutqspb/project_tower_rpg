using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct QuestTrackRequest : IComponentData
    {
        public Entity Unit;
        public FixedString64Bytes QuestId;   // пустая = снять трекинг
    }
}

using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct QuestAcceptRequest : IComponentData
    {
        public Entity Unit;
        public FixedString64Bytes QuestId;
    }
}

using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct QuestCompleteRequest : IComponentData
    {
        public Entity Unit;
        public FixedString64Bytes QuestId;
    }
}

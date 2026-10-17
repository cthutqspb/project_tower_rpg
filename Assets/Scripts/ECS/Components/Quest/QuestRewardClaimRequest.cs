using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct QuestRewardClaimRequest : IComponentData
    {
        public Entity Unit;
        public FixedString64Bytes QuestId;
        public int ChosenRewardIndex;   // -1 = без choice
    }
}

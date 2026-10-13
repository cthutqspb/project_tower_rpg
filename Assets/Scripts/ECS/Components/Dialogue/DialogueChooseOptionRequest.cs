using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct DialogueChooseOptionRequest : IComponentData
    {
        public Entity Player;
        public int OptionIndex;
    }
}

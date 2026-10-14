using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct DialogueChooseOptionRequest : IComponentData
    {
        public Entity Unit;
        public int OptionIndex;
    }
}

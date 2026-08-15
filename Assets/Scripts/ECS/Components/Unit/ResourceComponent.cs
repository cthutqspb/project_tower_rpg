using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public enum ResourceType
    {
        Mana,
        Energy,
        Rage,
        None
    }

    public struct ResourceComponent : IComponentData
    {
        public ResourceType Type;
        public float Current;
        public float Max;
    }
}


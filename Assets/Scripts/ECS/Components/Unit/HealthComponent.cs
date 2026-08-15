using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct HealthComponent : IComponentData
    {
        public float Current;
        public float Max;
    }
}


using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    [InternalBufferCapacity(16)]
    public struct PathBufferElement : IBufferElementData
    {
        public float3 Waypoint;
    }
}


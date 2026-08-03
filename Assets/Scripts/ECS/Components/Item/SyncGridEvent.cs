using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct SyncGridEvent : IComponentData
    {
        public Entity ContainerEntity;
        public int SlotIndex;
    }
}

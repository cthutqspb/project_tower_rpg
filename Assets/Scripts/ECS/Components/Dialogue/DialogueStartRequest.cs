using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct DialogueStartRequest : IComponentData
    {
        public Entity Unit;
        public Entity Npc;
    }
}

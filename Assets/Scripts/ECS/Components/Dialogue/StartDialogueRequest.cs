using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct StartDialogueRequest : IComponentData
    {
        public Entity Player;
        public Entity Npc;
    }
}

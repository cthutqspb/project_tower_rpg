using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct ClickIntent : IComponentData
    {
        // Живой Си-индекс того, кто физически инициировал взаимодействие (Эми или NPC)
        public Entity Actor;
    }
}



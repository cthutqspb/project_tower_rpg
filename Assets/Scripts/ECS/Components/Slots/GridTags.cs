using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Теги для различения типов контейнеров
    /// </summary>
    
    // Инвентарь (рюкзак, сундук, труп)
    public struct InventoryTag : IComponentData { }
    
    // Аурафрейм (баффы/дебаффы)
    public struct AuraFrameTag : IComponentData { }

    public struct PaperdollTag : IComponentData { }

    public struct ContainerTag : IComponentData { }
}

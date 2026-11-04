using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // Общий тег (если нужно найти вообще все интерактивные объекты)
    public struct InteractableTag : IComponentData { }

    // Теги конкретных типов
    public struct GateTag : IComponentData { }
    public struct LampTag : IComponentData { }
    // public struct ChestTag : IComponentData { }

    // Теги состояний
    public struct IsOpenTag : IComponentData { }
    public struct IsLockedTag : IComponentData { }
    public struct IsOnTag : IComponentData { }
}

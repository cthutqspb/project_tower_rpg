using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// 🚪 Одноразовый запрос на взаимодействие с объектом окружения (Дверь, Фонарь, Рычаг).
    /// Создается в InputSystem (через ActionResolver) и читается в InteractableSystem.
    /// </summary>
    public struct InteractRequest : IComponentData
    {
        public Entity Source; // Кто взаимодействует (Игрок)
        public Entity Target; // С чем взаимодействуют (Дверь)
    }
}

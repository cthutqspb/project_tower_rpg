using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Универсальный компонент-контейнер для слотов.
    /// Используется для инвентаря, экшенбара, аурафрейма и любых других сеток.
    /// </summary>
    public struct StaticGridComponent : IComponentData
    {
        public int SlotCount;
        public Entity Owner;
    }
}

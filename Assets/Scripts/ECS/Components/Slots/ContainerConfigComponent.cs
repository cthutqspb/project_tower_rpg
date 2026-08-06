using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public struct ContainerConfigComponent : IComponentData
    {
        public int Columns;
        public int Rows;
        public Entity Owner;
        
        // Быстрый геттер для общего количества ячеек в буфере
        public int SlotCount => Rows > 0 ? Columns * Rows : Columns;
    }
}


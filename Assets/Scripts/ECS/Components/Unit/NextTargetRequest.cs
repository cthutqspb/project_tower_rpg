using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // Запрос на поиск следующей цели (Tab, колесо мыши, геймпад — не важно).
    public struct NextTargetRequest : IComponentData
    {
        public Entity Unit;   // кому ставим цель (обычно игрок)
    }
}

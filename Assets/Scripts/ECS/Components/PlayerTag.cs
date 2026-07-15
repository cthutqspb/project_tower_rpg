using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 👑 ТЕГ ИГРОКА: Пустая структура-маркер. 
    // Нужна, чтобы системы ввода понимали, в какую именно сущность заливать вектор WASD!
    public struct PlayerTag : IComponentData {}
}


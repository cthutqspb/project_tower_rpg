using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    // 🛡️ СТРУКТУРА ДАННЫХ ДВИЖЕНИЯ:
    // По канону DOTS — это строго struct, наследуемая от IComponentData.
    // Память в ОЗУ будет идеально плоским Си-массивом!
    public struct MovementComponent : IComponentData
    {
        public float speed;       // Скорость перемещения (например, 5.0f)
        public float3 direction; // Текущий вектор направления (куда бежим)
    }
}


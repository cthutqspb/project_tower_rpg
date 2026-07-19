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
        public float cameraAngle; // УГОЛ КАМЕРЫ В РАДИАНАХ
        public bool isLookAroundMode;
        public bool isRmbOrMmbPressed;

         // 🔥 ДАННЫЕ ДЛЯ ФИЗИКИ ПРЫЖКА:
        public bool isGrounded;        // Стоит ли туша железно на земле
        public bool jumpRequested;     // Нажал ли игрок Пробел в этом кадре
    }
}


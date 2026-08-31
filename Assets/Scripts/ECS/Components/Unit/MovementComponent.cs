using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    // 🛡️ СТРУКТУРА ДАННЫХ ДВИЖЕНИЯ (Очищенный runtime-паспорт перемещения)
    // Все поля приведены к единому PascalCase стандарту C#!
    public struct MovementComponent : IComponentData
    {
        public float HitboxRadius; 
        // 🦾 НАШИ ПОДГОТОВЛЕННЫЕ ДАННЫЕ ДЛЯ СКОРОСТИ:
        public float BaseSpeed;          // Статический фундамент из JSON (запекается фабрикой)
        public float CurrentSpeed;       // Живая скорость с учетом шмоток/эффектов (считается системой стат)

        public float3 Direction;         // Текущий вектор направления (куда бежим)
        public float CameraAngle;        // Угол камеры в радианах
        public bool IsLookAroundMode;
        public bool IsRmbOrMmbPressed;

        // 🔥 ДАННЫЕ ДЛЯ ФИЗИКИ ПРЫЖКА:
        public bool IsGrounded;          // Стоит ли туша железно на земле
        public bool JumpRequested;       // Нажал ли игрок Пробел в этом кадре
    }
}



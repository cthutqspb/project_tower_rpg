using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    // 🧠 СТРУКТУРА ПАССИВНОГО ИИ-АВТОМАТА:
    // Хранит координаты дома и покадровые таймеры раздумий скелета.
    public struct AiComponent : IComponentData
    {   
        public bool IsFromFactory; 

        public float3 StartPoint;        // spawn_position (Точка дома для патруля)
        public float PatrolRadius;       // Наш лимит прогулки вокруг дома (±70 пикселей)
        public float3 CurrentTarget;     // ctx.ai_target_position (Куда сейчас топает)
        public float NextActionTime;     // ctx.ai_timer (Таймер раздумий / паузы)
        
        public bool HasTarget;           // Флаг: выбрал ли мозг точку
        public bool IsPatrolling;        // ctx.ai_is_patrolling (Флаг снижения скорости в патруле)
        
        // // TODO: BG3 / Pathfinder Utility AI взвешенных способностей на будущее
        // public Entity AiTargetUid;    // Боевой прицел на игрока
        // public float AgroRange;       // Радиус обнаружения
        // public float LooseRange;      // Радиус потери цели
    }
}


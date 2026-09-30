using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    public enum AiState : byte
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Dead
    }

    public struct AiComponent : IComponentData
    {
        public bool IsFromFactory;

        // Патруль
        public float3 StartPoint;
        public float PatrolRadius;
        public float3 PatrolPoint;
        public float NextActionTime;
        public bool HasPatrolTarget;
        //public bool IsPatrolling;

        // Стейт
        public AiState State;

        // Атака
        public float AttackTimer;          // задержка между ударами (как ctx.ai_timer)
        public float AttackRange;          // рассчитанный радиус атаки под текущую способность
        public FixedString32Bytes PrimaryAbility;   // текущая выбранная способность
    }
}

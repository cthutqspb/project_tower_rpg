using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public enum NameplateRelation : byte
    {
        Neutral = 0,
        Hostile = 1,
        Friendly = 2,
        Player = 3,
    }

    public struct CombatStateComponent : IComponentData
    {
        public bool IsInCombat;
        public Entity CurrentTarget;
        
        // 🆕 Цвет для UI (вычисляется в PerceptionSystem)
        public NameplateRelation Relation;

        public bool HasTarget => CurrentTarget != Entity.Null;

        public float GcdDuration;
        public float GcdRemaining;
    }
}

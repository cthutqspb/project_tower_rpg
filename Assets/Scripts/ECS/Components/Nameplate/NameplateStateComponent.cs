using Unity.Collections;
using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    public enum NameplateMark : byte
    {
        None = 0,
        Quest = 1,
        TurnIn = 2,   // задел
        // Gray = 3,     // задел
    }

    public struct NameplateStateComponent : IComponentData
    {
        public FixedString64Bytes NameKey;
        public NameplateMark Mark;   // 🆕 вместо bool
    }
}

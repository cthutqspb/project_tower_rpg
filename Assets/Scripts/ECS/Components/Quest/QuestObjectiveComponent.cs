using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Прогресс одной цели одного квеста. Буфер на юните.
    public struct QuestObjectiveComponent : IBufferElementData
    {
        public FixedString64Bytes QuestId;
        public FixedString64Bytes ObjectiveId;
        public int Current;
        public int Required;
        public bool IsCompleted;
    }
}

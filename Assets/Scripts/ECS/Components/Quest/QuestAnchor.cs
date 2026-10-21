using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Буфер на NPC: какие квесты он может дать или принять
    public struct QuestAnchor : IBufferElementData
    {
        public FixedString64Bytes QuestId;
    }
}

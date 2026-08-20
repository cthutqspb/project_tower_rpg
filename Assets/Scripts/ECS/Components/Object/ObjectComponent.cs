using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    public struct ObjectComponent : IComponentData
    {
        public FixedString64Bytes ObjectId;
        public FixedString64Bytes Type; // "door", "lever", "trap", "chest"
        public bool IsInteractable;
        public bool IsLocked;
    }
}

using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Состояние активной диалоговой сессии на игроке.
    // Живёт, пока диалог открыт. Умирает при закрытии.
    public struct DialogueStateComponent : IComponentData
    {
        public Entity Speaker;                    // NPC, с которым говорим
        public FixedString64Bytes CurrentNode;    // текущая нода графа
        public FixedString64Bytes EntryNode;      // с какой ноды начали сессию (для контекста)
        public FixedString64Bytes SpeakerNameKey;   
        public FixedString64Bytes TextKey;          
        public bool IsActive;
    }
}

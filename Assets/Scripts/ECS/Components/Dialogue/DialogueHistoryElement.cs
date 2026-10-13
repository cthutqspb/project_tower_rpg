using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // История выборов игрока в диалогах.
    // Живёт на игроке, сохраняется в сейв.
    public struct DialogueHistoryElement : IBufferElementData
    {
        public FixedString64Bytes SpeakerKey;   // "unit_banshee_zalina"
        public FixedString64Bytes NodeKey;      // "zalina_greet"
        public int OptionIndex;                 // индекс опции в ноде
    }
}

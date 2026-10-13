using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // Видимая опция диалога в текущей ноде.
    // Заполняется DialogueSystem, читается UI.
    public struct DialogueOptionElement : IBufferElementData
    {
        public FixedString64Bytes TextKey;        // ключ локализации
        public FixedString32Bytes SuffixKey;
        public int OptionIndex;                   // индекс в графе (для клика)
        public FixedString64Bytes NextNode;       // для отладки/UI (можно не использовать)
        public bool IsVisited;
    }
}

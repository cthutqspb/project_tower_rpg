using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Actions
{
    public struct ActionCommand : IComponentData
    {
        public ActionKind Action;        // "loot", "open_container", "attack", "interact", "move_to", "item_transfer", "item_drop", "item_use"
        public Entity SourceEntity;            // Кто выполняет действие
        public int SourceSlot;                 // Для трансферов/дропа
        public Entity TargetEntity;            // Над кем/чем выполняется действие
        public int TargetSlot;                 // Для трансферов (-1 = автоматический поиск)
        public FixedString64Bytes ItemId;      // ID предмета (для трансферов/дропа/использования)
        public int Amount;                     // Количество (для трансферов/дропа)
        public float3 Position;                // Для move_to и item_drop
    }
}

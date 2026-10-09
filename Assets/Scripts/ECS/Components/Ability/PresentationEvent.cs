using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // =========================================================================
    // 🦾 ГЛОБАЛЬНЫЙ ОПКОД СИГНАЛОВ ПРЕЗЕНТАЦИИ (Unmanaged byte, 1 байт в ОЗУ)
    // Единый шлюз для одновременного запуска 3D-эффектов, звуков и 2D-логов!
    // =========================================================================
    public enum PresentationEventKind : byte
    {
        None = 0,

        // 🧙‍♂️ МАГИЯ И БОЁВКА (0x10)
        Attack = 10,
        CastStart = 11,
        CastEnd = 12,
        Hit = 13,
        Death = 14,

        // 🔮 АУРЫ И ДЕБАФФЫ (0x20)
        AuraApplied = 20,
        AuraTick = 21,
        AuraEnd = 22,

        // 🎒 ПРЕДМЕТЫ И МИР (0x30)
        ItemLooted = 30,
        ItemDropped = 31,
        ItemCrafted = 32,
        ItemUsed = 33
    }

    // 🦾 СЕМАHТИЧЕСКИЕ СИ-ФАСАДЫ ДЛЯ ЧИСТОТЫ КOДА В РЕДЬЮСЕРАХ
    public static class CombatEvents
    {
        public const PresentationEventKind Attack = PresentationEventKind.Attack;
        public const PresentationEventKind CastStart = PresentationEventKind.CastStart;
        public const PresentationEventKind CastEnd = PresentationEventKind.CastEnd;
        public const PresentationEventKind Hit = PresentationEventKind.Hit;
        public const PresentationEventKind Death = PresentationEventKind.Death;
    }

    public static class AuraEvents
    {
        public const PresentationEventKind Applied = PresentationEventKind.AuraApplied;
        public const PresentationEventKind Tick = PresentationEventKind.AuraTick;
        public const PresentationEventKind End = PresentationEventKind.AuraEnd;
    }

    public static class WorldEvents
    {
        public const PresentationEventKind ItemLooted = PresentationEventKind.ItemLooted;
        public const PresentationEventKind ItemDropped = PresentationEventKind.ItemDropped;
        public const PresentationEventKind ItemCrafted = PresentationEventKind.ItemCrafted;
        public const PresentationEventKind ItemUsed = PresentationEventKind.ItemUsed;
    }

    public struct PresentationEvent : IBufferElementData
    {
        public PresentationEventKind Kind;
        public Entity Source;
        public Entity Target;
        public FixedString32Bytes Param; // Имя абилки "frostbolt" или ID шмотки "iron_sword"
    }

    public struct PresentationEventBufferTag : IComponentData { }
}


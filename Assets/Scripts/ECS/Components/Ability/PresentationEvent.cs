using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // 🦾 1. ЭНУМ ТИПОВ СОБЫТИЙ (Канон BG3 / WoW)
    public enum PresentationEventKind : byte
    {
        Attack,
        CastStart,
        CastEnd,
        Hit,
        Death,
        AuraApplied,
        AuraTick,
        AuraEnd
    }

    // 🦾 2. ПЛОСКАЯ СТРУКТУРА СОБЫТИЯ (Без ссылок на тяжелые GameObject)
    public struct PresentationEvent : IBufferElementData
    {
        public PresentationEventKind Kind;
        public Entity Source;
        public Entity Target;
        public FixedString32Bytes Param; // Например, имя абилки "frostbolt"
    }

    // 🦾 3. ТEГ-СИНГЛТОН ДЛЯ УДЕРЖАНИЯ КАРКАСА ШИНЫ СОБЫТИЙ В ECS
    public struct PresentationEventBufferTag : IComponentData { }
}


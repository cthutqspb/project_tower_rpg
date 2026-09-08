using Unity.Entities;
using Unity.Collections;

// =========================================================================
// 🧬 ЭЛЕМЕНТ БУФЕРА ДЛЯ ЮНИТА / ИГРОКА (Источник правды)
// =========================================================================
/// <summary>
/// Динамический буфер, который вешается на Юнита (игрока или моба).
/// Хранит все запущенные в данный момент кулдауны способностей/предметов этого юнита.
/// </summary>
public struct ActiveCooldownElement : IBufferElementData
{
    // FixedString32Bytes — эффективная ECS-строка для ID группы кулдауна (например, "potion_heal", "spell_fireball")
    public FixedString32Bytes CooldownGroup;
    public float Remaining;
    public float Duration;
}

// =========================================================================
// 🏷️ ТЕГИ-ПРИВЯЗКИ ДЛЯ ПРЕДМЕТОВ И СПОСОБНОСТЕЙ
// =========================================================================
/// <summary>
/// Вешается на префаб/сущность конкретной Способности или Предмета.
/// Указывает, к какой группе КД они принадлежат.
/// </summary>
public struct CooldownGroup : IComponentData
{
    public FixedString32Bytes Value;
}

/// <summary>
/// Базовая длительность кулдауна для этой конкретной способности/предмета.
/// </summary>
public struct BaseCooldownDuration : IComponentData
{
    public float Value;
}


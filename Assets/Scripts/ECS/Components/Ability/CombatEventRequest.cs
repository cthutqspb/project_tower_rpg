using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Всеядный unmanaged-запрос на боевое взаимодействие (Урон / Хил / Ауры)
    /// </summary>
    public struct CombatEventRequest : IComponentData
    {
        public Entity Caster;              // Кто применил способность
        public Entity Target;              // Кто принимает на грудь Си-байты эффекта
        public FixedString32Bytes AbilityId; // Жесткий unmanaged-хэш из JSON ("frostbolt")
    }
}


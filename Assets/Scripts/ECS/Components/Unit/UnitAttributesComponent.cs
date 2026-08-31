using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // =========================================================================
    // 🧬 ДОМЕН АТРИБУТОВ ЮНИТА (Единый unmanaged-паспорт характеристик в ОЗУ)
    // =========================================================================

    // 🦾 БАЗОВЫЕ АТРИБУТЫ (Голая туша): 
    // Меняются только при лвл-апе. Отсюда читает строго SaveManager!
    public struct UnitBaseAttributesComponent : IComponentData
    {
        public int strength;
        public int agility;
        public int intellect;
        public int wisdom;
        public int stamina;
    }

    // 🦾 ТЕКУЩИЕ АТРИБУТЫ (Одетая туша): 
    // Меняются динамически при смене шмота/баффах. Отсюда читают системы боя!
    public struct UnitCurrentAttributesComponent : IComponentData
    {
        public int strength;
        public int agility;
        public int intellect;
        public int wisdom;
        public int stamina;
    }
}


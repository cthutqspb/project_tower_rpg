using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🛡️ СТРУКТУРА БОЕВОГО СОСТОЯНИЯ:
    // По канону DOTS — это строго struct, наследуемая от IComponentData.
    // Хранит мутабельный паспорт здоровья в плоском Си-массиве ОЗУ.
    public struct CombatStateComponent : IComponentData
    {
        public bool IsDead;              // unit.combat.is_dead
        public bool IsInCombat;          // unit.combat.is_in_combat
        
        public int CurrentHp;            // health_resource.current
        public int MaxHp;                // health_resource.max
        
        public float BaseSpeed;          // parameters.base_speed
        public float CurrentSpeed;       // parameters.current_speed
        public float HitboxRadius;       // parameters.hitbox_radius

        // 🌟 СЕТЕВОЙ ЗАДЕЛ: Персональная цель конкретно ЭТОГО существа
        // (Игрок хранит тут скелета, а скелет в своей памяти будет хранить игрока!)
        public Entity CurrentTarget;

        // Быстрое свойство-помощник для проверки, есть ли у юнита цель
        public bool HasTarget => CurrentTarget != Entity.Null;

        // // TODO: WoW-Канон магии, агро-матрицы и вендетты на будущее
        // public Entity CombatTarget;   // combat_target_uid
        // public float GcdCurrent;      // cast.gcd_current
    }
}


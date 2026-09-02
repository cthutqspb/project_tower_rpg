using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // 🦾 ММО-ПАКЕТ ЗАПРОСА НА КАСT (Blittable Команда):
    // Рождается в UIInputSystem, за секунду пережёвывается боевым бэкендом и стирается.
    public struct CastRequest : IComponentData
    {
        public Entity Player;                // Ссылка на Entity игрока/актора, который прожал кнопку
        public int SlotIndex;                // Индекс нажатой ячейки экшенбара (0..11)
        public FixedString64Bytes AbilityId; // Текстовый ID способности ("frostbolt", "melee_attack")
        public Entity TargetEntity;          // Зафиксированная цель в миллисекунду нажатия по WoW-канону!
    }
}


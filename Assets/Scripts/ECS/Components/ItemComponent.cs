using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🛡️ ТВОЙ ПЕРВЫЙ ЧИСТОКРОВНЫЙ ECS-КОМПОНЕНТ ДАННЫХ ПРЕДМЕТА:
    public struct ItemComponent : IComponentData
    {
        public int itemId;       // Числовой хэш из базы предметов
        public int amount;       // Количество в стаке
        public int lootTableId;  // ID таблицы лута
        public bool isLooted;    // Наш вечный флаг обыска!
    }
}


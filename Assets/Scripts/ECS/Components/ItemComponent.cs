using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Главный компонент предмета в мире. Полный канон на интах.
    /// </summary>
    public struct ItemComponent : IComponentData
    {
        // Уникальный паспорт куба на карте. 
        // Вместо тяжелой строки делаем хэш от координат (как в твоем Defold: i_X_Y -> GetHashCode())
        public int Uid;

        // Числовой хэш предмета ("iron_sword".GetHashCode())
        public int ItemId;       

        public int Amount;       
        public int LootTableId;  
        public bool IsLooted;    
    }
}


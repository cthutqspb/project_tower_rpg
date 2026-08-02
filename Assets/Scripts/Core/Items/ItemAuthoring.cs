using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components; // ← Подключаем твой ЕДИНСТВЕННЫЙ каноничный компонент

namespace ProjectTowerRpg.Core.Items
{
    public class ItemAuthoring : MonoBehaviour
    {
        [Header("Настройки предмета")]
        public string itemId = "iron_sword"; 
        public int amount = 1;
        public string lootTableId = "empty";

        public Entity Entity { get; set; } = Entity.Null;

        public class Baker : Baker<ItemAuthoring>
        {
            public override void Bake(ItemAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Запекаем данные в твой единственный Pure ECS компонент
                AddComponent(entity, new ItemComponent 
                { 
                    Uid = $"i_{(int)authoring.transform.position.x}_{(int)authoring.transform.position.z}".GetHashCode(),
                    ItemId = authoring.itemId.GetHashCode(), 
                    Amount = authoring.amount,
                    LootTableId = authoring.lootTableId.GetHashCode(),
                    IsLooted = false
                });

                authoring.Entity = entity;
            }
        }
    }
}



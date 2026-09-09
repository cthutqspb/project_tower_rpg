using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

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
                
                // ✅ Передаём строку напрямую
                AddComponent(entity, new ItemComponent 
                { 
                    Uid = $"i_{(int)authoring.transform.position.x}_{(int)authoring.transform.position.z}".GetHashCode(),
                    ItemId = authoring.itemId,  // ← строка
                    Amount = authoring.amount,
                    LootTableId = authoring.lootTableId,  // ← строка
                    IsLooted = false
                });

                authoring.Entity = entity;
            }
        }
    }
}


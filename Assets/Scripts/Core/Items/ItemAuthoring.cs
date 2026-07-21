using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.Core.Items
{
    // Компонент данных, который будет жить в невидимом ОЗУ процессора (ECS)
    public struct ItemComponent : IComponentData
    {
        // В чистом ECS DOTS нельзя хранить динамические C# строки (string),
        // поэтому мы используем фиксированную строку FixedString32Bytes.
        // Она вмещает до 32 символов текста (наш "iron_sword" влезет с запасом).
        public FixedString32Bytes ItemId; 
    }

    // Скрипт-переводчик для редактора Unity
    public class ItemAuthoring : MonoBehaviour
    {
        [Header("Настройки предмета")]
        public string itemId = "iron_sword"; // ID из JSON файла

        public class Baker : Baker<ItemAuthoring>
        {
            public override void Bake(ItemAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Запекаем строковый ID предмета в чистую ECS память
                AddComponent(entity, new ItemComponent 
                { 
                    ItemId = authoring.itemId 
                });
            }
        }
    }
}


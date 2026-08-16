using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    public class ItemSpawnAuthoring : MonoBehaviour
    {
        [Header("Параметры Спавна Предмета из IDE")]
        public string itemId = "iron_sword";
        public int amount = 1;

        // 🎨 ВИЗУАЛИЗАЦИЯ МЕТКИ В РЕДАКТОРЕ UNITY (Каноничный неоновый Tokyonight Green)
        private void OnDrawGizmos()
        {
            // Рисуем куб точно в пивоте объекта, копейка в копейку как у юнитов!
            Color tokyonightGreen = new Color(0.41f, 0.86f, 0.54f, 0.70f); // Приятный неоновый зелёный
            Gizmos.color = tokyonightGreen;
            Gizmos.DrawCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));
            
            Gizmos.color = new Color(0.41f, 0.86f, 0.54f, 1.0f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));

            // Для предметов стрелка направления взгляда обычно не нужна, 
            // но для идеальной семантики размеров и центровки оставляем тонкую зелёную ось
            Gizmos.color = new Color(0.41f, 0.86f, 0.54f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }    
    }

    public class ItemSpawnBaker : Baker<ItemSpawnAuthoring>
    {
        public override void Bake(ItemSpawnAuthoring authoring)
        {
            // Возвращаем TransformUsageFlags.None — маркер исчезнет, его личный трансформ в ECS не нужен
            var entity = GetEntity(TransformUsageFlags.None);

            // 🚀 ГЕНИАЛЬНОЕ СХЛОПЫВАНИЕ: Вместо нового компонента-маркера 
            // мы сразу генерируем стандартный запрос на спавн предмета в мире!
            AddComponent(entity, new DropItemRequest
            {
                ItemId = authoring.itemId,
                Amount = authoring.amount,
                Position = authoring.transform.position
            });
        }
    }
}


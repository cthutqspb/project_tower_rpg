using System.Collections.Generic;
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

        [Header("ID таблицы лута")]
        public string lootTableId = "";

        [Header("Кастомный лут (Если таблица пустая)")]
        public List<CustomLootItem> customLoot = new List<CustomLootItem>();

        [Header("Время респавна объекта (в СЕКУНДАХ, 0 = без респавна)")]
        public int respawnTime = 0;

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
            var markerEntity = GetEntity(TransformUsageFlags.None);

            AddComponent(markerEntity, new ItemSpawnMarkerComponent
            {
                ItemId = authoring.itemId,
                Amount = authoring.amount,
                SpawnPosition = authoring.transform.position,
                LootTableId = authoring.lootTableId,
                RespawnTime = authoring.respawnTime,
            });

            // 🦾 Если геймдизайнер набил кастомный лут — кладём его в буфер ItemSlot на маркере
            if (authoring.customLoot != null && authoring.customLoot.Count > 0)
            {
                var markerBuffer = AddBuffer<ItemSlot>(markerEntity);
                foreach (var lootItem in authoring.customLoot)
                {
                    if (string.IsNullOrEmpty(lootItem.itemId)) continue;
                    markerBuffer.Add(new ItemSlot
                    {
                        DataId = lootItem.itemId,
                        Amount = math.max(1, lootItem.amount),
                    });
                }
            }
        }
    }
}


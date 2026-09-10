using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    [Serializable]
    public struct CustomLootItem
    {
        public string itemId;
        public int amount;
    }

    public class UnitSpawnAuthoring : MonoBehaviour
    {
        [Header("Параметры Спавна из IDE")]
        public string unitId = "skeleton_warrior";
        public int level = 1;
        public string rank = "common"; // common, rare, elite, boss
        public string lootTableId = "";
        public bool isPlayer = false;

        public bool isDead = false;

        [Header("Кастомный лут (Если таблица пустая)")]
        public List<CustomLootItem> customLoot = new List<CustomLootItem>();

        // 🎨 ВИЗУАЛИЗАЦИЯ МЕТКИ В РЕДАКТОРЕ UNITY (Каноничный неоновый Tokyonight Red)
        private void OnDrawGizmos()
        {
            // Убрали смещение + 0.3f по Y. Теперь куб рисуется точно в пивоте объекта!
            Color tokyonightRed = new Color(0.97f, 0.46f, 0.56f, 0.70f);
            Gizmos.color = tokyonightRed;
            Gizmos.DrawCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));
            
            Gizmos.color = new Color(0.97f, 0.46f, 0.56f, 1.0f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));

            // Стрелка взгляда теперь тоже выходит из честного центра
            Gizmos.color = new Color(0.49f, 0.81f, 1.0f, 1.0f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.72f);
        }    
    }

    public class UnitSpawnBaker : Baker<UnitSpawnAuthoring>
    {
        public override void Bake(UnitSpawnAuthoring authoring)
        {
            // Получаем сущность КУБА-МАРКЕРА запроса спавна
            var markerEntity = GetEntity(TransformUsageFlags.None);

            // 1. Запекаем плоские ТТХ и ID таблицы лута в маркер
            AddComponent(markerEntity, new UnitSpawnMarkerComponent
            {
                PrefabEntity = Entity.Null, 
                UnitId = authoring.unitId,
                Level = authoring.level,
                Rank = authoring.rank,
                SpawnPosition = authoring.transform.position,
                IsPlayer = authoring.isPlayer,
                IsDead = authoring.isDead,
                LootTableId = authoring.lootTableId // Теперь маркер знает таблицу!
            });

            // 2. 🦾 ЗАПЕКАНИЕ КАСТOМНOГO ЛУТА: 
            // Если геймдизайнер добавил шмотки через плюс в инспекторе, 
            // мы вешаем буфер ItemSlot прямо на КУБ-МАРКЕР как временный Си-контейнер!
            if (string.IsNullOrEmpty(authoring.lootTableId) && authoring.customLoot != null && authoring.customLoot.Count > 0)
            {
                var markerBuffer = AddBuffer<ItemSlot>(markerEntity);
                
                foreach (var lootItem in authoring.customLoot)
                {
                    if (string.IsNullOrEmpty(lootItem.itemId)) continue;

                    markerBuffer.Add(new ItemSlot
                    {
                        DataId = lootItem.itemId, // Записали FixedString64Bytes ID предмета
                        Amount = math.max(1, lootItem.amount)
                    });
                }
            }
        }
    }
   
}


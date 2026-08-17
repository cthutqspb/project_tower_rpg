using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    public class UnitSpawnAuthoring : MonoBehaviour
    {
        [Header("Параметры Спавна из IDE")]
        public string unitId = "skeleton_warrior";
        public int level = 1;
        public string rank = "common"; // common, rare, elite, boss
        public bool isPlayer = false;

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
            // Возвращаем TransformUsageFlags.None — маркеру в рантайме не нужен свой ECS-трансформ
            var entity = GetEntity(TransformUsageFlags.None);

            // Переносим сухие данные из Unity-инспектора в плоскую Си-структуру
            AddComponent(entity, new UnitSpawnMarkerComponent
            {
                PrefabEntity = Entity.Null, // Больше не храним здесь ссылки на запеченные префабы!
                UnitId = authoring.unitId,
                Level = authoring.level,
                Rank = authoring.rank,
                SpawnPosition = authoring.transform.position,
                IsPlayer = authoring.isPlayer
            });
        }
    }
}


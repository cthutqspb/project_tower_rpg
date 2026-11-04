using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    public class InteractableSpawnAuthoring : MonoBehaviour
    {
        [Header("Настройки в инспекторе")]
        public string interactableId = ""; 
        public InteractableType type = InteractableType.Gate;
        public bool startOpen = false;
        
        [Header("Визуальный префаб (если пусто — используется сам объект)")]
        public GameObject visualPrefab; 

        [Header("Отладка в редакторе")]
        [Tooltip("Показывать зеленый куб в редакторе (включи для пустых маркеров, выключи для реальных ворот)")]
        public bool drawGizmo = true; // 🎯 ТОТ САМЫЙ ФЛАГ

        private void OnDrawGizmos()
        {
            // Рисуем куб только если флаг включен
            if (!drawGizmo) return;

            Color tokyonightGreen = new Color(0.41f, 0.86f, 0.54f, 0.70f);
            Gizmos.color = tokyonightGreen;
            Gizmos.DrawCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));
            
            Gizmos.color = new Color(0.41f, 0.86f, 0.54f, 1.0f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.27f, 0.27f, 0.27f));
        }

        private void Awake()
        {
            // В игре мы всегда выключаем рендеры у оригинала, 
            // чтобы EntityVisibilitySystem заспавнила рабочую копию.
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;
            
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;
        }

        public class Baker : Baker<InteractableSpawnAuthoring>
        {
            public override void Bake(InteractableSpawnAuthoring authoring)
            {
                var markerEntity = GetEntity(TransformUsageFlags.None);

                GameObject prefabToSpawn = authoring.visualPrefab != null ? authoring.visualPrefab : authoring.gameObject;

                AddComponent(markerEntity, new InteractableSpawnMarkerComponent
                {
                    Id = authoring.interactableId,
                    Type = authoring.type,
                    StartOpen = authoring.startOpen,
                    SpawnPosition = authoring.transform.position,
                    PrefabRef = new UnityObjectRef<GameObject> { Value = prefabToSpawn }
                });
            }
        }
    }
}

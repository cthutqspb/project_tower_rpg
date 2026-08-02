using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.Core.Items
{
    public class ItemVisualizer : MonoBehaviour
    {
        [SerializeField] private GameObject _itemPrefab; 

        private HashSet<Entity> _spawnedEntities = new HashSet<Entity>();

        private void Update()
        {
            // 1. Проверяем, создался ли вообще ECS мир симуляции (Live World) во время игры
            if (World.DefaultGameObjectInjectionWorld == null) return;
            
            // Ищем мир живой симуляции рантайма (Live World) по флагам Unity 6
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && (world.Flags & WorldFlags.Simulation) == 0)
            {
                foreach (var w in World.All)
                {
                    if ((w.Flags & WorldFlags.Simulation) != 0)
                    {
                        world = w;
                        break;
                    }
                }
            }

            if (world == null) return;
            var em = world.EntityManager;


            // 2. Используем чистый и каноничный SystemAPI.Query прямо внутри MonoBehaviour!
            // Для этого мы временно переключаем контекст выполнения на мир симуляции
            using (var query = em.CreateEntityQuery(typeof(ItemComponent), typeof(LocalTransform)))
            {
                if (query.IsEmpty) return;

                var entitiesArray = query.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var currentEntity in entitiesArray)
                {
                    // Если для этой ECS-сущности из ОЗУ мы еще не спавнили куб в Главной Сцене
                    if (!_spawnedEntities.Contains(currentEntity))
                    {
                        var transformData = em.GetComponentData<LocalTransform>(currentEntity);

                        // Берём чистые координаты из ECS
                        Vector3 targetPosition = (Vector3)transformData.Position;
                        Quaternion targetRotation = (Quaternion)transformData.Rotation;

                        // 3. Спавним ГРАФИКУ. Так как этот скрипт висит на [ItemFactory] в Главной Сцене,
                        // Instantiate гарантированно создаст куб в SampleScene, а не внутри сабсцены!
                        GameObject visualCube = Instantiate(_itemPrefab, targetPosition, targetRotation);
                        
                        if (visualCube.TryGetComponent<ItemAuthoring>(out var authoring))
                        {
                            authoring.Entity = currentEntity;
                        }
                        // Фиксируем связь в чистом реестре графики
                        EntityRegistry.RegisterItemVisual(currentEntity, visualCube);
                        _spawnedEntities.Add(currentEntity);

                        Debug.Log($"[ItemVisualizer] Графика куба успешно создана в Главной Сцене для Entity {currentEntity.Index} на {targetPosition}");
                    }
                }

                entitiesArray.Dispose();
            }

            // Убираем из кэша сущности, которые были удалены из ECS ОЗУ (слутаны)
            _spawnedEntities.RemoveWhere(entity => !em.Exists(entity));
        }
    }
}


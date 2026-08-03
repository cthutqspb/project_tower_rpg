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
                // ВАЖНО: Больше не пишем return! Если query пустой — мы просто пропускаем цикл спавна, 
                // но позволяем коду спуститься ниже к логике удаления графики!
                if (!query.IsEmpty) 
                {
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

                            // 3. Спавним ГРАФИКУ.
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
            }

            // 🎯 Физическое уничтожение графики при удалении сущности из ECS ОЗУ
System.Collections.Generic.List<Entity> toRemove = new System.Collections.Generic.List<Entity>();

foreach (Entity entity in _spawnedEntities)
{
    if (!em.Exists(entity))
    {
        toRemove.Add(entity);
        
        // Нативно вытаскиваем игровой объект куба из нашего реестра
        GameObject cubeObject = EntityRegistry.GetItemVisual(entity);
        if (cubeObject != null)
        {
            // Насильно стираем куб со сцены в реальном времени!
            Destroy(cubeObject);
            
            // Зачищаем реестр, чтобы не копить утечки памяти
            EntityRegistry.UnregisterItemVisual(entity);
        }
    }
}

// Безопасно очищаем наш HashSet
foreach (Entity entity in toRemove)
{
    _spawnedEntities.Remove(entity);
}



        }
    }
}


using Unity.Entities;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class InteractableSpawnSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();

            // Проходимся по всем маркерам, которые Бейкер создал в сцене
            foreach (var (marker, entity) in SystemAPI.Query<RefRO<InteractableSpawnMarkerComponent>>().WithEntityAccess())
            {   
                UnityEngine.Debug.Log($"🔍 [InteractableSpawnSystem] Найден маркер {marker.ValueRO.Id}!");
                // 1. Создаем ECS-сущность для логики и визуала
                Entity interactableEntity = ecb.CreateEntity();
                
                ecb.AddComponent(interactableEntity, new InteractableComponent 
                { 
                    Id = marker.ValueRO.Id, 
                    Type = marker.ValueRO.Type,
                    IsInteractable = true,
                    PrefabRef = marker.ValueRO.PrefabRef // 🎯 ПЕРЕДАЕМ ССЫЛКУ, А НЕ ИМЯ
                });

                // Задаем позицию, которую мы взяли из префаба на сцене
                ecb.AddComponent(interactableEntity, LocalTransform.FromPosition(marker.ValueRO.SpawnPosition));
                
                // Вешаем теги в зависимости от типа
                if (marker.ValueRO.Type == InteractableType.Gate || marker.ValueRO.Type == InteractableType.Door) 
                    ecb.AddComponent<GateTag>(interactableEntity);
                
                // Если дверь должна быть открыта по умолчанию
                if (marker.ValueRO.StartOpen) 
                    ecb.AddComponent<IsOpenTag>(interactableEntity);
                
                UnityEngine.Debug.Log($"🟢 [InteractableSpawnSystem] Создана сущность ворот {marker.ValueRO.Id}. Маркер удален.");
                // 2. Уничтожаем сущность-маркер, она больше не нужна
                ecb.DestroyEntity(entity);
            }
        }
    }
}

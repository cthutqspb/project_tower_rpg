using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class InteractableSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();

            // 🚪 Ловим все запросы на взаимодействие
            foreach (var (request, entity) in SystemAPI.Query<RefRO<InteractRequest>>().WithEntityAccess())
            {
                Entity target = request.ValueRO.Target;
                
                // Проверка безопасности: цель существует и у неё есть компонент интерактивности
                if (target == Entity.Null || !SystemAPI.Exists(target) || !SystemAPI.HasComponent<InteractableComponent>(target))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var interactable = SystemAPI.GetComponent<InteractableComponent>(target);

                // =========================================================================
                // 🧠 МОЗГ: Обработка логики в зависимости от типа объекта
                // =========================================================================
                switch (interactable.Type)
                {
                    case InteractableType.Gate:
                    case InteractableType.Door:
                        HandleDoor(ecb, target);
                        break;

                    case InteractableType.Lamp:
                        HandleLamp(ecb, target);
                        break;
                    
                    // В будущем здесь появятся Case для Lever, Chest и т.д.
                }

                // Уничтожаем сущность-запрос, так как мы её обработали
                ecb.DestroyEntity(entity);
            }
        }

        // =========================================================================
        // 🚪 ЛОГИКА ДВЕРИ: Переключение состояния (Открыто/Закрыто)
        // =========================================================================
        private void HandleDoor(EntityCommandBuffer ecb, Entity target)
        {
            // Если на двери уже есть IsOpenTag — значит она открыта, закрываем её
            if (SystemAPI.HasComponent<IsOpenTag>(target))
            {
                ecb.RemoveComponent<IsOpenTag>(target);
                Debug.Log($"[InteractableSystem]: Дверь {target.Index} ЗАКРЫТА.");
            }
            else
            {
                // Иначе — открываем
                ecb.AddComponent<IsOpenTag>(target);
                Debug.Log($"[InteractableSystem]: Дверь {target.Index} ОТКРЫТА.");
            }
        }

        // =========================================================================
        // 💡 ЛОГИКА ФОНАРЯ: Переключение света (Вкл/Выкл)
        // =========================================================================
        private void HandleLamp(EntityCommandBuffer ecb, Entity target)
        {
            // Если на фонаре уже есть IsOnTag — значит он горит, выключаем
            if (SystemAPI.HasComponent<IsOnTag>(target))
            {
                ecb.RemoveComponent<IsOnTag>(target);
                Debug.Log($"[InteractableSystem]: Фонарь {target.Index} ВЫКЛЮЧЕН.");
            }
            else
            {
                ecb.AddComponent<IsOnTag>(target);
                Debug.Log($"[InteractableSystem]: Фонарь {target.Index} ВКЛЮЧЕН.");
            }
        }
    }
}

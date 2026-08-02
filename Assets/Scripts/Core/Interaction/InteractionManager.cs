using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.ECS.Systems;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.Interaction
{
    public class InteractionManager : MonoBehaviour
    {
        private EntityManager _em;
        private Camera _camera;

        private void Start()
        {
            _camera = GetComponent<Camera>();
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

       private void Update()
{
    // 1. Проверяем само нажатие мыши
    if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
    
    // Пишем лог, что клик вообще зарегистрирован системой ввода
    Debug.Log($"[InteractionManager] Клик ЛКМ обнаружен. Позиция мыши: {Mouse.current.position.ReadValue()}");

    if (DragManager.Instance.IsDragging) 
    {
        Debug.Log("[InteractionManager] Клик пропущен: сейчас идет перетаскивание предмета.");
        return;
    }

    Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
    
    // 2. Стреляем толстым лучом
    if (Physics.SphereCast(ray, 0.3f, out RaycastHit hit, 100f))
    {
        // Лог 2: Луч во что-то попал в 3D мире
        Debug.Log($"[InteractionManager] 🟢 Попадание луча в объект: '{hit.collider.gameObject.name}' на слое {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

        var authoring = hit.collider.GetComponent<ItemAuthoring>();
        
        if (authoring != null)
        {
            // Лог 3: Нашли нужный скрипт авторинга
            Debug.Log($"[InteractionManager] 🎯 На объекте найден ItemAuthoring! Связанная Entity ID: {authoring.Entity.Index}");

            if (authoring.Entity != Entity.Null)
            {
                Entity clickedEntity = authoring.Entity;

                if (_em.HasComponent<ProjectTowerRpg.ECS.Components.ItemComponent>(clickedEntity))
                {
                    Entity inventoryEntity = EntityRegistry.Get("unit_inventory");
                    SendActionCommand("item_loot", clickedEntity, inventoryEntity);
                }
                else
                {
                    Debug.LogWarning($"[InteractionManager] ⚠️ У сущности {clickedEntity.Index} отсутствует компонент ItemComponent в ОЗУ!");
                }
            }
            else
            {
                Debug.LogWarning("[InteractionManager] 🔴 Ошибка: У ItemAuthoring переменная Entity равна Entity.Null. Запекание не сработало!");
            }
        }
        else
        {
            Debug.Log("[InteractionManager] ⚪ На задетом объекте нет скрипта ItemAuthoring.");
        }
    }
    else
    {
        // Лог 4: Луч улетел в пустоту неба
        Debug.Log("[InteractionManager] 🔴 Луч улетел в пустоту, ни одного коллайдера не задето.");
    }
}


        private void SendActionCommand(string type, Entity source, Entity target)
        {
            Entity actionEntity = _em.CreateEntity();
            _em.AddComponentData(actionEntity, new ActionCommand
            {
                Type = type,
                SourceEntity = source,
                TargetEntity = target,
                SourceSlot = -1,
                TargetSlot = -1,
                ItemId = "",
                Amount = 0
            });
            Debug.Log($"[InteractionManager] Сгенерирована команда {type}: Источник={source.Index}, Цель={target.Index}");
        }
    }
}


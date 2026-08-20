using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewLinkSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 1. Находим все ItemView на сцене
            var viewsOnScene = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
            if (viewsOnScene.Length == 0) return;

            // 2. Находим все сущности предметов в ECS
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            var entities = query.ToEntityArray(Allocator.Temp);

            // 3. Связываем по UID
            foreach (var entity in entities)
            {
                var itemData = em.GetComponentData<ItemComponent>(entity);
                int currentUid = itemData.Uid;

                foreach (var view in viewsOnScene)
                {
                    // Пропускаем уже привязанные
                    if (view.Entity != Entity.Null) continue;

                    // 🔥 ГЛАВНОЕ: сравниваем по UID (хеш строки)
                    int viewUid = view.uid.GetHashCode();

                    if (viewUid == currentUid)
                    {
                        view.Entity = entity;
                        view.IsLinked = true;
                        Debug.Log($"[ItemViewLinkSystem] Связан {view.gameObject.name} (uid: {view.uid}) с Entity {entity.Index}");
                        break;
                    }
                }
            }

            entities.Dispose();
        }
    }
}

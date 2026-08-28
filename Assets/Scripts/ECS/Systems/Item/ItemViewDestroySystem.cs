using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.ECS.Components; // Твой домен компонентов

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewDestroySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Находим все кубы предметов на сцене через их ItemView
            var itemsOnScene = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
            if (itemsOnScene.Length == 0) return;

            foreach (var view in itemsOnScene)
            {
                if (view.Entity != Entity.Null)
                {
                    // 🦾 ИСТИННЫЙ ECS-ГВАРД УДАЛЕНИЯ ГРАФИКИ:
                    // Если сущность предмета ЕЩЕ существует в ECS, НО с неё уже пропал 
                    // маркер материализации StoredTag (значит, редьюсер Loot её успешно собрал в сумку!)
                    // ИЛИ если сущность была полностью стёрта из памяти симуляции (!em.Exists) — 
                    // во всех этих случаях 3D-тело куба ОБЯЗАНО исчезнуть со сцены мгновенно!
                    bool isLooted = em.Exists(view.Entity) && em.HasComponent<StoredTag>(view.Entity);
                    bool isDestroyed = !em.Exists(view.Entity);

                    if (isLooted || isDestroyed)
                    {
                        Debug.Log($"🧹 [ItemViewDestroySystem]: Предмет {view.itemId} (Entity {view.Entity.Index}) собран в рюкзак или стёрт. Аннигилирую 3D-куб {view.gameObject.name} со сцены!");
                        
                        // Насильно стираем куб с экрана, очищая Mono-кучу
                        Object.Destroy(view.gameObject);
                    }
                }
            }
        }
    }
}


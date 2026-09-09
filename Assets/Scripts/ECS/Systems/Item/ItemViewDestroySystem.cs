using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

// 🚀 ХИРУРГИЧЕСКИЙ РАЗВOД ИМПОРТОВ (Wow-Канон систем-мостов):
// Мы полностью выжгли using UnityEngine;, забрав только 3 точечных класса!
using Object = UnityEngine.Object;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewDestroySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // ⚠️ ВНИМАНИЕ: Оставляем FindObjectsByType строго для текущих тестов!
            // В будущем этот поиск намертво выжигается через кверение unmanaged-компонента ссылки на ассет!
            var itemsOnScene = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
            if (itemsOnScene.Length == 0) return;

            foreach (var view in itemsOnScene)
            {
                // Нагло убираем костыльную проверку == null, используя C# null-conditional оператор
                if (view == null || view.Entity == Entity.Null) continue;

                // 🦾 ИСТИННЫЙ ECS-ГВАРД УДАЛЕНИЯ ГРАФИКИ:
                bool isLooted = em.Exists(view.Entity) && em.HasComponent<StoredTag>(view.Entity);
                bool isDestroyed = !em.Exists(view.Entity);

                if (isLooted || isDestroyed)
                {
                    // Лог пишется чисто, лаконично и без инлайн-префиксов
                    Debug.Log($"🧹 [ItemViewDestroySystem]: Предмет {view.itemId} (Entity {view.Entity.Index}) собран или стёрт. Аннигилирую 3D-куб {view.gameObject.name}!");
                    
                    // Насильно стираем куб с экрана, очищая Mono-кучу
                    Object.Destroy(view.gameObject);
                }
            }
        }
    }
}


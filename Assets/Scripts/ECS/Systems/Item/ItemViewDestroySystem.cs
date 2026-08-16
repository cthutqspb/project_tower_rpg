using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Systems
{
    // Работает в той же группе инициализации, подчищая хвосты за прошлый кадр
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewDestroySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 🎯 НАХОДИМ ВСЕ КУБЫ ПРЕДМЕТОВ НА СЦЕНЕ (Через твой новый ItemView!)
            var itemsOnScene = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
            if (itemsOnScene.Length == 0) return;

            foreach (var view in itemsOnScene)
            {
                // 🌟 СИММЕТРИЧНЫЙ ФИКС: Проверяем свойство Entity с большой буквы
                if (view.Entity != Entity.Null && !em.Exists(view.Entity))
                {
                    Debug.Log($"🧹 [ItemViewDestroySystem]: Сущность {view.Entity.Index} удалена из ECS. Уничтожаю 3D-куб {view.gameObject.name} со сцены!");
                    
                    // Насильно стираем куб с экрана
                    Object.Destroy(view.gameObject);
                }
            }
        }
    }
}


using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items; // Подключаем твой домен предметов

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewLinkSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 🎯 1. НАХОДИМ ВСЕ МОДЕЛЬКИ ПРЕДМЕТОВ НА СЦЕНЕ (Через чистый ItemView!)
            var viewsOnScene = Object.FindObjectsByType<ItemView>(FindObjectsInactive.Exclude);
            if (viewsOnScene.Length == 0) return; 

            // 🎯 2. ДЕЛАЕМ ЗАПРОС В ECS: Берем живые души предметов из ОЗУ
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            var entities = query.ToEntityArray(Allocator.Temp);

            // 🚀 ЗЕРКАЛЬНЫЙ КОНВЕЙЕР СВЯЗЫВАНИЯ СУЩНОСТЕЙ С ГРАФИКОЙ
            foreach (var entity in entities)
            {
                var itemData = em.GetComponentData<ItemComponent>(entity);
                string currentItemId = itemData.ItemId.ToString();
                int currentUid = itemData.Uid;

                foreach (var authoring in viewsOnScene)
                {
                    // Проверяем замок: если этот предмет уже привязан к какой-то сущности в ECS — скипаем
                    if (authoring.Entity != Entity.Null) continue;

                    // Генерируем уникальный UID по текущим координатам куба (Копейка в копейку как в твоем Бэйкере!)
                    int authoringUid = $"i_{(int)authoring.transform.position.x}_{(int)authoring.transform.position.z}".GetHashCode();

                    // Проверяем совпадение: либо совпал уникальный UID координат, либо строковый ID из базы
                    bool isUidMatch = authoringUid == currentUid;
                    bool isIdMatch = authoring.itemId == currentItemId;

                    if (isUidMatch || isIdMatch)
                    {
                        // 🌟 ЦЕМЕНТИРУЕМ СВЯЗЬ: Записываем живую ECS-сущность прямо в свойство!
                        authoring.Entity = entity; 

                        Debug.Log($"📦 [ItemViewLinkSystem]: Предмет {authoring.gameObject.name} успешно слинкован с Entity ID: {entity.Index}!");
                        break;
                    }
                }
            }

            entities.Dispose();
        }
    }
}


using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    // Система работает в фазе инициализации кадра
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class UnitViewLinkSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 🎯 1. НАХОДИМ ВСЕ СВОБОДНЫЕ 3D-МОДЕЛЬКИ НА СЦЕНЕ
            // 🌟 ИСПРАВЛЕНО ДЛЯ UNITY 6: Убран FindObjectsSortMode, используем новый чистый метод
            var viewsOnScene = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
            if (viewsOnScene.Length == 0) return; // Если все модельки уже привязаны — выходим

            // 🎯 2. ДЕЛАЕМ ЗАПРОС В ECS: Нам нужны все новые живые души юнитов
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<UnitComponent>());
            var entities = query.ToEntityArray(Allocator.Temp);

            // 🚀 УЛЬТИМАТИВНЫЙ ДВУХСТОРОННИЙ КОНВЕЙЕР СВЯЗЫВАНИЯ (0% ХАРДКОДА)
            foreach (var entity in entities)
            {
                var unitData = em.GetComponentData<UnitComponent>(entity);
                string currentUid = unitData.Uid.ToString();
                string currentUnitId = unitData.UnitId.ToString();

                // Бежим по моделькам на сцене и ищем ту, которая совпадает по ID паспорта!
                foreach (var view in viewsOnScene)
                {
                    // Проверяем: если эта моделька еще не инициализирована И совпала по типу (или по UID чанка)
                    if (!view.IsLinked && (view.unitId == currentUnitId || view.uid == currentUid))
                    {
                        // Намертво цементируем связь марионетки с её ECS-сущностью!
                        view.GetComponent<SyncTransformWithEntity>().Initialize(entity);
                        view.IsLinked = true; // Помечаем, что моделька занята
                        break;
                    }
                }
            }

            entities.Dispose();
        }
    }
}


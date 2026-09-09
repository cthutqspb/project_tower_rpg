using Unity.Entities;

// 🚀 ХИРУРГИЧЕСКИЙ РАЗВOД ИМПОРТОВ: Забираем строго 3 нужных нам managed-класса!
using Object = UnityEngine.Object;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class UnitViewDestroySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Находим марионеток монстров на сцене через их UnitView
            // ⚠️ Напоминание: этот покадровый поиск по сцене — временный контур для текущих тестов!
            var unitsOnScene = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
            if (unitsOnScene.Length == 0) return;

            foreach (var view in unitsOnScene)
            {
                if (view == null || view.entity == Entity.Null) continue;

                // Если сущность монстра была полностью стёрта из памяти симуляции (!em.Exists) — 
                // мы обязаны мгновенно убрать его 3D-тело со сцены!
                if (!em.Exists(view.entity))
                {
                    Debug.Log($"🧹 [UnitViewDestroySystem]: Сущность юнита {view.entity.Index} удалена из ECS. Аннигилирую 3D-тело {view.gameObject.name}!");
                    
                    // Насильно стираем куб/модельку с экрана, очищая Mono-кучу
                    Object.Destroy(view.gameObject);
                }
            }
        }
    }
}


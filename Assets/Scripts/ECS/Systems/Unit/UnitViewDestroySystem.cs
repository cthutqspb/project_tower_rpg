using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;

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
            var unitsOnScene = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
            if (unitsOnScene.Length == 0) return;

            foreach (var view in unitsOnScene)
            {
                // Если сущность монстра была полностью стёрта из памяти симуляции (!em.Exists) — 
                // мы обязаны мгновенно убрать его 3D-тело со сцены!
                if (view.entity != Entity.Null && !em.Exists(view.entity))
                {
                    Debug.Log($"🧹 [UnitViewDestroySystem]: Сущность юнита {view.entity.Index} удалена из ECS. Аннигилирую 3D-тело {view.gameObject.name}!");
                    Object.Destroy(view.gameObject);
                }
            }
        }
    }
}


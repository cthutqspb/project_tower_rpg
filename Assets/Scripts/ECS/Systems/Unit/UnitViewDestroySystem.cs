using Unity.Entities;
using ProjectTowerRpg.Core.Presentation;

using Object = UnityEngine.Object;
using Debug  = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class UnitViewDestroySystem : SystemBase
    {
        private readonly System.Collections.Generic.List<Entity> _deadEntities = new();

        protected override void OnUpdate()
        {
            _deadEntities.Clear();

            CollectDeadUnits();
            DestroyDeadUnits();
        }

        private void CollectDeadUnits()
        {
            var em = EntityManager;

            foreach (var entity in EntityViewRegistry.GetEntities())
            {
                if (!EntityViewRegistry.TryGet<UnitView>(entity, out _)) continue;

                // Юнит умер — сущность стёрта из ECS.
                if (!em.Exists(entity))
                    _deadEntities.Add(entity);
            }
        }

        private void DestroyDeadUnits()
        {
            for (int i = 0; i < _deadEntities.Count; i++)
            {
                Entity entity = _deadEntities[i];

                if (!EntityViewRegistry.TryGet<UnitView>(entity, out var view)) continue;

                Debug.Log(
                    $"🧹 [UnitViewDestroySystem]: Сущность юнита {entity.Index} удалена из ECS. " +
                    $"Аннигилирую 3D-тело {view.gameObject.name}!");

                Object.Destroy(view.gameObject);
                EntityViewRegistry.Unregister(entity);
            }
        }
    }
}

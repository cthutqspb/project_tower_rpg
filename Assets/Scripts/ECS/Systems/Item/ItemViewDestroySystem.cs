using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Presentation;

using Object = UnityEngine.Object;
using Debug  = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemViewDestroySystem : SystemBase
    {
        private readonly System.Collections.Generic.List<Entity> _deadEntities = new();

        protected override void OnUpdate()
        {
            _deadEntities.Clear();

            CollectDeadItems();
            DestroyDeadItems();
        }

        // ── Проход 1: только чтение реестра ──────────────────────────────
        private void CollectDeadItems()
        {
            var em = EntityManager;

            foreach (var entity in EntityViewRegistry.GetEntities())
            {
                if (!EntityViewRegistry.TryGet<ItemView>(entity, out var view)) continue;
                if (view.entity == Entity.Null) continue;

                bool exists = em.Exists(view.entity);

                // Сущности нет — предмет уже удалён где-то ещё.
                if (!exists)
                {
                    _deadEntities.Add(entity);
                    continue;
                }

                // Сущность есть, но висит StoredTag — предмет подобрали.
                if (em.HasComponent<StoredTag>(view.entity))
                {
                    _deadEntities.Add(entity);
                }
            }
        }

        // ── Проход 2: уничтожение ────────────────────────────────────────
        private void DestroyDeadItems()
        {
            for (int i = 0; i < _deadEntities.Count; i++)
            {
                Entity entity = _deadEntities[i];

                if (!EntityViewRegistry.TryGet<ItemView>(entity, out var view)) continue;

                Debug.Log(
                    $"🧹 [ItemViewDestroySystem]: Предмет {view.itemId} " +
                    $"(Entity {view.entity.Index}) собран или стёрт. " +
                    $"Аннигилирую 3D-куб {view.gameObject.name}!");

                Object.Destroy(view.gameObject);
                EntityViewRegistry.Unregister(entity);
            }
        }
    }
}

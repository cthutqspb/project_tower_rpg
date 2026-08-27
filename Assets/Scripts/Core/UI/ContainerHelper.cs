using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI
{
    public static class ContainerHelper
    {
        /// <summary>
        /// Находит контейнер (инвентарь/куклу/экшенбар и т.д.) для указанного юнита по тегу.
        /// </summary>
        /// <typeparam name="T">Тег контейнера (InventoryTag, PaperdollTag, etc.)</typeparam>
        /// <param name="unitEntity">Сущность юнита-владельца</param>
        /// <param name="em">EntityManager</param>
        /// <returns>Entity контейнера или Entity.Null, если не найден</returns>
        public static Entity GetContainerForUnit<T>(Entity unitEntity, EntityManager em) where T : unmanaged, IComponentData
        {
            if (unitEntity == Entity.Null || !em.Exists(unitEntity))
                return Entity.Null;

            // Запрос на все контейнеры с нужным тегом
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ContainerConfigComponent>(),
                ComponentType.ReadOnly<ItemSlot>(),
                ComponentType.ReadOnly<T>()
            );

            using var entities = query.ToEntityArray(Allocator.Temp);
            
            foreach (var container in entities)
            {
                var config = em.GetComponentData<ContainerConfigComponent>(container);
                if (config.Owner == unitEntity)
                    return container;
            }

            return Entity.Null;
        }

        /// <summary>
        /// Находит все контейнеры юнита (без привязки к конкретному тегу).
        /// </summary>
        public static NativeArray<Entity> GetAllContainersForUnit(Entity unitEntity, EntityManager em, Allocator allocator = Allocator.Temp)
        {
            if (unitEntity == Entity.Null || !em.Exists(unitEntity))
                return new NativeArray<Entity>(0, allocator);

            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ContainerConfigComponent>(),
                ComponentType.ReadOnly<ItemSlot>()
            );

            var allContainers = query.ToEntityArray(allocator);
            var result = new NativeList<Entity>(allocator);

            foreach (var container in allContainers)
            {
                var config = em.GetComponentData<ContainerConfigComponent>(container);
                if (config.Owner == unitEntity)
                    result.Add(container);
            }

            allContainers.Dispose();
            return result.ToArray(allocator);
        }

        /// <summary>
        /// Проверяет, существует ли контейнер с указанным тегом для юнита.
        /// </summary>
        public static bool HasContainerForUnit<T>(Entity unitEntity, EntityManager em) where T : unmanaged, IComponentData
        {
            return GetContainerForUnit<T>(unitEntity, em) != Entity.Null;
        }
    }
}

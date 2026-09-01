using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🦾 СВЕРХЗВУКОВОЙ ПАСПОРТ СВЯЗЕЙ С БУФЕРАМИ ЮНИТА (По твоему канону):
    // Намертво запекается фабрикой при спавне, полностью аннигилируя лапшу лукапов!
    public struct BuffersLinkComponent : IComponentData
    {
        public Entity Inventory; // Ссылка на сущность-сателлит с буфером рюкзака (StaticGrid)
        public Entity Paperdoll; // Ссылка на сущность-сателлит с буфером куклы одежды (Paperdoll)
    }
}


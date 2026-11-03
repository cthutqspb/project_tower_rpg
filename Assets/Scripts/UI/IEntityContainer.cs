using Unity.Entities;

namespace ProjectTowerRpg.UI
{
    public interface IEntityContainer
    {
        Entity BoundEntity { get; }
    }
}

using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public interface IEntityContainer
    {
        Entity BoundEntity { get; }
    }
}

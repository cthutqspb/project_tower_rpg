using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public interface IEcsUiComponentReceiver<T> where T : unmanaged, IComponentData
    {
        void UpdateFromComponent(ref T component);
    }
}

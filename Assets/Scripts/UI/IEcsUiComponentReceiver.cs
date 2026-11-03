using Unity.Entities;

namespace ProjectTowerRpg.UI
{
    public interface IEcsUiComponentReceiver<T> where T : unmanaged, IComponentData
    {
        void UpdateFromComponent(ref T component);
    }
}

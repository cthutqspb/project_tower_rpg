using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public interface IEcsUiBufferReceiver<T> where T : unmanaged, IBufferElementData
    {
        void UpdateFromBuffer(DynamicBuffer<T> buffer);
    }
}

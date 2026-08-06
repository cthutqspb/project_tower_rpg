using Unity.Entities;

public interface IEcsUiBufferReceiver<T> where T : unmanaged, IBufferElementData
{
    // Обязательное свойство сущности, к которой привязан этот UI-элемент
    Entity BoundEntity { get; }
    
    void BindToEntity(Entity entity);
    void UpdateFromBuffer(DynamicBuffer<T> buffer);
}


using Unity.Entities;

public interface IEcsUiBufferReceiver<T> where T : unmanaged, IBufferElementData
{
    // Обязательное свойство сущности, к которой привязан этот UI-элемент
    Entity BoundEntity { get; }    
    void BindToEntity(Entity entity);

    // 🦾 Параметры isOnlyValidation/gcdRemaining/gcdDuration/cooldowns
    // имеют смысл ТОЛЬКО для слотовых буферов (ItemSlot, ActionBarSlot, AuraSlot).
    // Для остальных (QuestComponent, QuestObjectiveComponent) — игнорируются.
    // Так сделано ради полиморфизма StaticGrid, который обслуживает несколько буферов.
    void UpdateFromBuffer(
        DynamicBuffer<T> buffer,
        bool isOnlyValidation = false,
        float gcdRemaining = 0f,
        float gcdDuration = 0f,
        DynamicBuffer<ActiveCooldownElement> cooldowns = default
    );
}


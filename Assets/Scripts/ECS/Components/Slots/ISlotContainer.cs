using Unity.Entities;

public interface ISlotContainer
{
    Entity Entity { get; }
    
    bool HasContent(int slot);
    object GetContent(int slot);
    void SetContent(int slot, object content);
    
    int FindEmptySlot();
    void ClearSlot(int slot);
    
    // Универсальный полиморфный метод валидации для ВСЕГО (предметы, спеллы, ауры)
    bool CanPlaceContent(int slot, object content);
}


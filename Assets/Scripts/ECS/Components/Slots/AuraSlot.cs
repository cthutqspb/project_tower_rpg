using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct AuraSlot : IBufferElementData
    {
        public FixedString32Bytes AbilityId; // "frost_armor", "ignite" (для выуживания Nerd Font и цвета из JSON)
        
        public float TimeRemaining;          // Сколько секунд осталось тикать ауре
        public float Duration;               // Полная длительность (нужно для расчета полоски/круга спада)
        public int Stacks;                   // Количество стаков (1, 2, 3...)
        
        public Entity CasterEntity;          // Кто повесил бафф/дебафф (чтобы сервер знал, чей инт считать при тике урона)

        public bool IsEmpty => AbilityId.IsEmpty;
    }
}


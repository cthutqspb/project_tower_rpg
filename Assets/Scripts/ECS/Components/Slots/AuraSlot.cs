using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct AuraSlot : IBufferElementData
    {   
        public int SlotIndex;
        public FixedString32Bytes AuraId; // "frost_armor", "ignite" (для выуживания Nerd Font и цвета из JSON)
        
        public float TimeRemaining;          // Сколько секунд осталось тикать ауре
        public float Duration;               // Полная длительность (нужно для расчета полоски/круга спада)
        public int Stacks;                   // Количество стаков (1, 2, 3...)
        
        public Entity CasterEntity;          // Кто повесил бафф/дебафф (чтобы сервер знал, чей инт считать при тике урона)
        public Entity TargetEntity;

        public float TickTimer; 

        public bool IsEmpty => AuraId.IsEmpty;

        // 🦾 ТОЧЕЧНОЕ САМООЧИЩЕНИЕ: Анатомия и Си-ссылка на хозяина (TargetEntity) ЗАЦЕМЕНТИРОВАНЫ!
        // Метод стирает только динамический payload протухшего баффа
        public void ClearContent()
        {
            AuraId = default;
            TimeRemaining = 0f;
            Duration = 0f;
            Stacks = 0;
            CasterEntity = Entity.Null;
            TickTimer = 0f;
            // TargetEntity и SlotIndex НЕ ТРОГАЕМ, они вечны для этого контейнера!
        }

        // 🦾 ТОЧЕЧНЫЙ СЕТТЕР КОНТЕНТА: Универсальный шлюз под рельсы твоего UI-менеджмента
        public void SetContent(object content)
        {
            if (content is AuraSlot data)
            {
                AuraId = data.AuraId;
                TimeRemaining = data.TimeRemaining;
                Duration = data.Duration;
                Stacks = data.Stacks;
                CasterEntity = data.CasterEntity;
                TargetEntity = data.TargetEntity;
                TickTimer = data.TickTimer;
            }
        }
    }
}


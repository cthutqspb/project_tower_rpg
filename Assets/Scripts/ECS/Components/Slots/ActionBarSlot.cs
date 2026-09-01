using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct ActionBarSlot : IBufferElementData
    {
        public int SlotIndex;    // 0..11 локальный индекс на панели
        
        public FixedString64Bytes AbilityId; // "frostbolt", "melee_attack" или "crystal_sword"
        public FixedString64Bytes SlotType;  // "spell" или "item"
        
        public bool IsEmpty => AbilityId.IsEmpty;

        // 🦾 ТОЧЕЧНОЕ САМООЧИЩЕНИЕ ПАНЕЛИ: Индексы кнопок 1..12 никогда не собирутся
        public void ClearContent()
        {
            AbilityId = "";
            SlotType = "";
        }

        // 🦾 ТОЧЕЧНЫЙ СЕТТЕР КОНТЕНТА: Междоменный драг (забирает и шмотку, и спелл!)
        public void SetContent(object content)
        {
            if (content is ItemSlot item)
            {
                AbilityId = item.DataId;
                SlotType = item.DataType;
            }
            else if (content is ActionBarSlot bar)
            {
                AbilityId = bar.AbilityId;
                SlotType = bar.SlotType;
            }
        }
    }
}


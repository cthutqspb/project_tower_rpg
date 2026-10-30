using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace ProjectTowerRpg.ECS.Components
{
    [GeneratePropertyBag]
    public struct ActionBarSlot : IBufferElementData
    {
        public int SlotIndex;    // 0..11 локальный индекс на панели
        
        public FixedString32Bytes AbilityId; // "frostbolt", "melee_attack" или "crystal_sword"
        public SlotContentType ContentType;
        
        // 🦾 ММО-КАНОН WOW: Физическая клавиша привязки хранится прямо на сервере в ОЗУ чанка!
        // По умолчанию при создании персонажа сервер запекает сюда дефолты ("1", "2", ... "-", "="),
        // а если игрок меняет настройки в меню — сервер просто точечно перезаписывает эту Си-строку!
        public FixedString32Bytes KeyBinding; 

        public bool IsEmpty => AbilityId.IsEmpty;

        public void ClearContent()
        {
            AbilityId = "";
            ContentType = SlotContentType.Empty;
            // KeyBinding МЫ НЕ СТИРАЕМ! Кнопка "1" остается привязанной к слоту, даже если мы убрали оттуда фаербол!
        }

        public void SetContent(object content)
        {
            if (content is ItemSlot item)
            {
                AbilityId = item.DataId;
                ContentType = item.ContentType;
            }
            else if (content is ActionBarSlot bar)
            {
                AbilityId = bar.AbilityId;
                ContentType = bar.ContentType;
                // При драг-энд-дропе скилла из ячейки в ячейку бинд кнопки ("1") остается на месте, 
                // мы переносим только контент!
            }
        }
    }
}


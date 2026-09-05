using Unity.Entities;
using Unity.Collections;

namespace ProjectTowerRpg.ECS.Components
{
    // 🦾 ВСЕЯДНЫЙ СИ-КОМПОНЕНТ КАСTА (CastComponent):
    // Можно повесить на игрока, босса, пилон в мире или юзаемую вещь!
    public struct CastComponent : IComponentData
    {
        public bool IsActive;                    // Флаг: идет ли каст прямо сейчас
        public FixedString64Bytes AbilityId;    // Строковый ID способности из JSON базы данных ("frostbolt")
        
        public float CastTime;                   // Полное эталонное время каста из базы (например, 1.7)
        public float Progress;                   // Текущее покадрово тикающее время каста в секундах (от 0 до Duration)
        public bool IsChanneling;                // Флаг потокового заклинания (wow-канон: полоска убывает справа налево)
    }
}


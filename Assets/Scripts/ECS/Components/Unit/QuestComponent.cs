using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.Core.Quest;

namespace ProjectTowerRpg.ECS.Components
{
    // 🦾 СИ-МОНОЛИТ: Идеальная симметрия с UnitComponent и HealthComponent!
    // Никаких слотов, никакой мусорной терминологии. Чистый ECS-буфер квестов.
    public struct QuestComponent : IBufferElementData
    {
        public FixedString64Bytes QuestId; // Уникальный строковый ID ("quest_zalina_casino")
        public QuestState State; // Стейт (Active, Completed...)
        public bool IsTracked;
        public bool RewardClaimed;
    }
}


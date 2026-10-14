using System;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Quest
{
    [Serializable]
    public enum QuestState : byte
    {
        Locked = 0,
        Available = 1,
        Active = 2,
        ReadyToTurnIn = 3,
        Completed = 4
    }

    [Serializable]
    public class QuestObjective
    {
        public string id;              // ID цели ("resolve_casino")
        public string text_key;        // ключ локализации текста цели
        public int required_amount;    // сколько надо сделать
    }

    [Serializable]
    public class QuestRewardItem
    {
        public string item_id;         // "crystal_sword"
        public int amount = 1;
    }

    [Serializable]
    public class QuestConfig
    {
        public string id;
        public string title_key;
        public string desc_key;
        public List<QuestObjective> objectives;
        public List<QuestRewardItem> rewards;
    }
}

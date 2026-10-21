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
        Completed = 3,
        TurnedIn = 4
    }

    [Serializable]
    public class QuestObjective
    {
        public string id;                              // ID цели
        public string type;                            // "kill" / "talk" / "discover" / "collect" / "use"
        public string text_key;                        // ключ локализации
        public int required_amount;                    // сколько надо сделать
        public Dictionary<string, string> identity;      // свободные пары фильтра
    }

    [Serializable]
    public class QuestReward
    {
        public string kind;    // "item", "experience", "currency", "ability", ...
        public string id;
        public int amount = 1;
        public bool is_choice; // 🆕 true = участвует в выборе, false = гарантированная
    }

    [Serializable]
    public class QuestConfig
    {
        public string id;
        public string title_key;
        public string desc_key;
        public string giver_name_key;
        public List<QuestObjective> objectives;
        public List<QuestReward> rewards;   // 🆕 один список
    }
}

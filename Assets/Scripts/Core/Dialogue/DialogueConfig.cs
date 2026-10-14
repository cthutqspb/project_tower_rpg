using System;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Dialogue
{
    // ============================================================
    // УСЛОВИЯ
    // ============================================================
    // type = "quest"  → quest_id + required_state
    // type = "flag"   → flag_id + flag_value
    // type = "skill"  → skill_id + skill_dc + skill_bonus_source
    // (остальные добавим потом: item, race, level)
    [Serializable]
    public class DialogueCondition
    {
        public string type;

        // quest
        public string quest_id;
        public string required_state;

        // flag
        public string flag_id;
        public bool flag_value;

        // skill
        public string skill_id;      // "persuasion", "intimidation", ...
        public int skill_dc;         // сложность
    }

    // ============================================================
    // ДЕЙСТВИЯ
    // ============================================================
    // type = "quest_accept"    → quest_id
    // type = "quest_complete"  → quest_id
    // type = "dialogue_close"
    // type = "set_flag"        → flag_id + flag_value
    // type = "clear_flag"      → flag_id
    [Serializable]
    public class DialogueAction
    {
        public string type;
        public string quest_id;
        public string flag_id;
        public bool flag_value;
    }

    // ============================================================
    // ОПЦИЯ (кнопка ответа игрока)
    // ============================================================
    [Serializable]
    public class DialogueOption
    {
        public string text_key;

        public List<DialogueCondition> conditions;   // видимость кнопки
        public List<DialogueAction> actions;         // что сделать при клике

        public string next_node;                     // куда идти после клика
    }

    // ============================================================
    // УЗЕЛ ГРАФА
    // ============================================================
    // type = "dialogue"   → text_key + options
    // type = "condition"  → branches + default_next (без текста)
    [Serializable]
    public class DialogueNode
    {
        public string type;                          // "dialogue" | "condition"

        // dialogue
        public string text_key;
        public List<DialogueOption> options;

        // condition
        public List<DialogueConditionBranch> branches;
        public string default_next;
    }

    // Ветка ноды-условия: если condition сошёлся → next_node
    [Serializable]
    public class DialogueConditionBranch
    {
        public string type;                          // "quest" | "flag" | "skill"
        public string quest_id;
        public string required_state;
        public string flag_id;
        public bool flag_value;
        public string skill_id;
        public int skill_dc;

        public string next_node;
    }

    // ============================================================
    // ГРАФ ДИАЛОГА
    // ============================================================
    [Serializable]
    public class DialogueGraph
    {
        public string start_node;                    // всегда "start_routing" (или как назовёшь)
        public Dictionary<string, DialogueNode> nodes;
    }
}

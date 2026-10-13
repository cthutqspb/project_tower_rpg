using System;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Quest
{
    [Serializable]
    public enum QuestState : byte
    {
        Locked = 0,       // Еще не доступен
        Available = 1,    // Можно взять у NPC
        Active = 2,       // В процессе выполнения (висит в журнале)
        ReadyToTurnIn = 3,// Цели выполнены, можно сдать
        Completed = 4     // Сдан, награда получена
    }

    [Serializable]
    public class QuestObjective
    {
        public string id;            // ID цели, например "kill_anatoliy" или "talk_anatoliy"
        public string text_key;      // Ключ локализации для текста задачи ("Разогнать казино Анатолия")
        public int current_amount;   // Сколько уже сделали
        public int required_amount;  // Сколько надо сделать
        public bool is_completed;
    }

    [Serializable]
    public class QuestConfig
    {
        public string id;            // Уникальный ID квеста ("quest_zalina_casino")
        public string title_key;     // Ключ локализации названия ("Элитная недвижимость")
        public string desc_key;      // Ключ локализации описания лора задачи
        public List<QuestObjective> objectives; // Список подзадач (массив целей)
    }
}


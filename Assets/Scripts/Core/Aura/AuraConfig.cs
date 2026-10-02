using System;
using UnityEngine;

namespace ProjectTowerRpg.Core.Auras
{
    [Serializable]
    public class AuraConfig
    {
        public string id;
        public string action_type; // "aura"
        public string type;        // "buff" или "debuff"
        public int max_stacks;
        public AuraIdentity identity;
        public AuraVisuals visuals;
        public AuraModifiers modifiers;
    }

    [Serializable]
    public class AuraIdentity
    {
        public string name_key;
        public string desc_key;
    }

    [Serializable]
    public class AuraVisuals
    {
        public string texture;
        public string animation;
        public string icon_char; // Символ-значок Nerd Font для SlotElement!
        public float[] color;    // RGBA массив [1.0, 0.4, 0.4, 1.0]
    }

    [Serializable]
    public class AuraModifiers
    {
        public float armor_multiplier = 1.0f;        // 1.5 = +50% брони
        public float attack_speed_modifier = 0.0f;   // -0.25 = -25% скорости атаки
    }
}


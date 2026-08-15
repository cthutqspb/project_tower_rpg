using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTowerRpg.Core.Items
{
    [Serializable]
    public class ItemIdentity
    {
        public string name_key;
        public string desc_key;
        public string type;       // weapon, armor, potion, container, etc.
        public string quality;    // common, rare, epic...
        public int price;
        public float weight;
        public int durability;
    }

    [Serializable]
    public class ItemVisuals
    {
        public string animation;
        public string texture;
        public int tile_index;
        public float[] color; // Наш массив [r, g, b, a] для JSON

        // Удобный геттер для Unity: собирает Color из массива на лету
        public Color GetUnityColor()
        {
            if (color == null || color.Length < 4) return Color.white;
            return new Color(color[0], color[1], color[2], color[3]);
        }
    }

    [Serializable]
    public class ItemProperties
    {
        public string equip_slot;   // HEAD, MAIN_HAND
        public string weapon_type;  // one_hand_sword, staff
        public string armor_type;   // leather, cloth
        public bool stackable;
        public int max_stack;
        public bool triggers_gcd;
        
        // Поля для сундуков/мешков (nullable типы, могут быть null, чтобы экономить RAM)
        [System.NonSerialized] public int? columns; 
        [System.NonSerialized] public int? rows;
    }

    [Serializable]
    public class ItemRequirements
    {
        public int level;
        public int strength;
        public int agility;
        public int intellect;
        public string resource; // "mana" или null
    }

    [Serializable]
    public class ValueRange
    {
        public float min;
        public float max;
    }

    [Serializable]
    public class ItemAttributes
    {
        public int strength;
        public int intellect;
        public int agility;
        public int stamina;
    }

    [Serializable]
    public class DamageConfig
    {
        public float min;
        public float max;
        public string type; // physical, arcane
    }

    [Serializable]
    public class ItemCombatStats
    {
        public ItemAttributes attributes;
        public DamageConfig damage;
        public List<DamageConfig> bonus_damage; // Массив доп. урона
        [System.NonSerialized] public Dictionary<string, int> resists; 
        public int armor_rating;
    }

    [Serializable]
    public class ItemPassiveEffect
    {
        public string aura_id;
        public float value;
        [System.NonSerialized] public int? chance;
    }

    [Serializable]
    public class ItemUseEffect
    {
        public string ability_id;
        public ValueRange value; // Наш универсальный диапазон
        public int cooldown;
    }

    // 👑 ПОЛНЫЙ ПАСПОРТ ПРЕДМЕТА:
    [Serializable]
    public class ItemConfig
    {
        public string id; // Вшивается автоматически при парсинге, как data.id = id_str в Lua!
        public string action_type;
        public ItemIdentity identity;
        public ItemVisuals visuals;
        public ItemProperties properties;
        public ItemRequirements requirements;
        public ItemCombatStats combat_stats;
        public List<ItemPassiveEffect> effects;
        public List<ItemUseEffect> use_effects;
    }
}


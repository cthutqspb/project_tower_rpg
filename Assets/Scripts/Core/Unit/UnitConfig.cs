using System;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Units
{
    [Serializable]
    public class UnitIdentity
    {
        public string name_key;
        public string race;
        public string faction;
        public string type;
        public string default_rank;
        
        // В Lua у тебя: unit_class = { warrior = true }. 
        // Чтобы Newtonsoft.Json сожрал это за 0 наносекунд, пишем стандартную Dictionary карту!
        public List<string> unit_class;
        public string loot_table_id;
    }

    [Serializable]
    public class UnitAttributes
    {
        public int strength;
        public int agility;
        public int intellect;
        public int wisdom;
        public int stamina;
    }

    [Serializable]
    public class UnitParameters
    {
        public int base_health;
        public float base_speed;
        public float hitbox_radius;
    }

    [Serializable]
    public class UnitProgression
    {
        public float growth_health;
        public float growth_damage;
    }

    [Serializable]
    public class UnitResource
    {
        public string type; // "mana", "rage"
        public int current;
        public int max;
    }

    [Serializable]
    public class UnitVisuals
    {
        public string animation;
        public string texture;
    }

    [Serializable]
    public class UnitAI
    {
        public string profile;
        public float base_aggro_radius;
        public bool is_ranged;
        public float flee_range;
        
        // Твоя Lua-таблица весов ["damage"] = 1.5 намертво ложится в C# Dictionary!
        [System.NonSerialized] public Dictionary<string, float> tag_weights;
    }

    // 👑 ПОЛНЫЙ СТАТИЧЕСКИЙ ПАСПОРТ МОНСТРА (Симметрично твоему ItemConfig):
    [Serializable]
    public class UnitConfig
    {
        public string id; // Вшивается автоматически в цикле при парсинге JSON
        public UnitIdentity identity;
        public UnitAttributes attributes;
        public UnitParameters parameters;
        public UnitProgression progression;
        public UnitResource resource;
        public UnitVisuals visuals;
        public UnitAI ai;
        public List<string> abilities;
    }
}


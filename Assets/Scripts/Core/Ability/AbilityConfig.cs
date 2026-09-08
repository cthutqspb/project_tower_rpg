using System;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Abilities
{
    [Serializable]
    public class AbilityConfig
    {
        public string id;
        public string action_type; // "ability"
        public AbilityIdentity identity;
        public AbilityVisuals visuals;
        public AbilityDelivery delivery;
        public AbilityParameters parameters;
        public AbilityRequirements requirements;
        public AbilityCostConfig cost;
        public List<AbilityEffectConfig> effects; // Полиморфный массив эффектов
    }

    [Serializable]
    public class AbilityIdentity
    {
        public string name_key;
        public string desc_key;
        public string @class; // class — зарезервированное слово C#, экранируем через @
        public List<string> tags;
    }

    [Serializable]
    public class AbilityVisuals
    {
        public string animation;
        public string texture;
        public int tile_index;
        public string icon_char;
    }

    [Serializable]
    public class AbilityDelivery
    {
        public string type; // "projectile", "instant_hit", "beam", "aoe"
        public string factory_url;
        public AbilityDeliveryFX fx;
    }

    [Serializable]
    public class AbilityDeliveryFX
    {
        public string sprite_animation;
        public string particle_fx;
        public string hit_fx;
        public float duration; // Для лучей
    }

    [Serializable]
    public class AbilityParameters
    {
        public string cooldown_group;
        public float range;
        public float speed;
        public float cast_time;
        public float cooldown;
        public bool triggers_gcd;
        public bool is_homing;
        public bool requires_target;
        public bool is_channeling;
    }

    [Serializable]
    public class AbilityRequirements
    {
        public int level;
        public string @class;
    }

    [Serializable]
    public class AbilityCostConfig
    {
        public string resource; // "mana", "rage", "energy", "health"
        public float value;
    }

    [Serializable]
    public class AbilityEffectConfig
    {
        public string type; // "direct_damage", "direct_heal", "apply_aura", "drain_resource"
        public string school;
        public float min;
        public float max;
        public float weapon_multiplier;
        [System.NonSerialized] public Dictionary<string, float> scaling_stats; // Динамические коэффициенты статов
        public string aura_id;
        public float value; // Длительность баффа/дебаффа или сила ауры
    }
}


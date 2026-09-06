using UnityEngine;

namespace ProjectTowerRpg.Core.UI.Colors
{
    public static class SolarizedOsakaNight
    {
        // 🌙 OSAKA NIGHT (акцентные)
        public static Color Bg => new Color(26f / 255f, 27f / 255f, 38f / 255f, 1f);
        public static Color Surface => new Color(36f / 255f, 40f / 255f, 59f / 255f, 1f);
        public static Color SurfaceLight => new Color(45f / 255f, 47f / 255f, 68f / 255f, 1f);
        public static Color Text => new Color(192f / 255f, 202f / 255f, 245f / 255f, 1f);
        public static Color TextDim => new Color(86f / 255f, 95f / 255f, 137f / 255f, 1f);
        public static Color Purple => new Color(187f / 255f, 154f / 255f, 247f / 255f, 1f);
        public static Color Blue => new Color(122f / 255f, 162f / 255f, 247f / 255f, 1f);
        public static Color Cyan => new Color(125f / 255f, 207f / 255f, 255f / 255f, 1f);
        public static Color Green => new Color(158f / 255f, 206f / 255f, 106f / 255f, 1f);
        public static Color Orange => new Color(255f / 255f, 158f / 255f, 100f / 255f, 1f);
        public static Color Red => new Color(247f / 255f, 118f / 255f, 142f / 255f, 1f);
        public static Color Yellow => new Color(224f / 255f, 175f / 255f, 104f / 255f, 1f);

        // 🧩 СЕМАНТИЧЕСКИЕ АЛИАСЫ
        public static Color Background => Bg;
        public static Color SurfaceColor => Surface;
        public static Color TextColor => Text;
        public static Color TextDimColor => TextDim;
        public static Color Primary => Blue;
        public static Color Success => Green;
        public static Color Warning => Yellow;
        public static Color Danger => Red;
        public static Color Info => Cyan;
        public static Color Accent => Purple;

        // ================================================================
        // 🎨 ЦВЕТА КАЧЕСТВА ПРЕДМЕТОВ
        // ================================================================
        public static Color GetQualityColor(string quality)
        {
            return quality?.ToLower() switch
            {
                "rare" => Primary,
                "uncommon" => Success,
                "epic" => Purple,
                "legendary" => Orange,
                "artifact" => Accent,
                _ => Text
            };
        }

        // ================================================================
        // 🎨 ЦВЕТА КЛАССОВ СПОСОБНОСТЕЙ
        // ================================================================
        public static Color GetAbilityColor(string classType)
        {
            return classType?.ToLower() switch
            {
                "mage" => Cyan,
                "warlock" => Purple,
                "priest" => Text,
                "warrior" => Danger,
                "rogue" => Yellow,
                "hunter" => Success,
                "shaman" => Cyan,
                "druid" => Success,
                "paladin" => Primary,               
                "monk" => Info,
                "bard" => Accent,
                _ => Text
            };
        }
    }
}

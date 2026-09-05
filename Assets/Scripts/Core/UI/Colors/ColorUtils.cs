using UnityEngine;

namespace ProjectTowerRpg.Core.UI.Colors
{
    public static class ColorUtils
    {
        // ================================================================
        // 🎨 ПРОЗРАЧНОСТЬ
        // ================================================================
        public static Color SetAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        // ================================================================
        // 🎨 ЗАТЕМНЕНИЕ / ОСВЕТЛЕНИЕ
        // ================================================================
        public static Color Darken(this Color color, float amount = 0.5f)
        {
            amount = Mathf.Clamp01(amount);
            return new Color(
                color.r * (1f - amount),
                color.g * (1f - amount),
                color.b * (1f - amount),
                color.a
            );
        }

        public static Color Lighten(this Color color, float amount = 0.5f)
        {
            amount = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Min(color.r + amount, 1f),
                Mathf.Min(color.g + amount, 1f),
                Mathf.Min(color.b + amount, 1f),
                color.a
            );
        }

        // ================================================================
        // 🎨 НАСЫЩЕННОСТЬ
        // ================================================================
        public static Color Desaturate(this Color color, float amount = 0.5f)
        {
            amount = Mathf.Clamp01(amount);
            float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(
                Mathf.Lerp(color.r, gray, amount),
                Mathf.Lerp(color.g, gray, amount),
                Mathf.Lerp(color.b, gray, amount),
                color.a
            );
        }

        // ================================================================
        // 🎨 ИНВЕРТИРОВАНИЕ
        // ================================================================
        public static Color Invert(this Color color)
        {
            return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
        }

        // ================================================================
        // 🎨 СМЕШИВАНИЕ (BLEND)
        // ================================================================
        public static Color Blend(this Color baseColor, Color overlay, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            return new Color(
                Mathf.Lerp(baseColor.r, overlay.r, alpha),
                Mathf.Lerp(baseColor.g, overlay.g, alpha),
                Mathf.Lerp(baseColor.b, overlay.b, alpha),
                Mathf.Lerp(baseColor.a, overlay.a, alpha)
            );
        }
    }
}

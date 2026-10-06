using UnityEngine;

namespace ProjectTowerRpg.Core.Colors
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


        // =========================================================================
        // 🔮 ШКОЛЫ МАГИИ (Для 2D-интерфейса слотов и 3D-эффектов в мире)
        // =========================================================================
        private const float GlowIntensity = 7.2f;

        public struct AuraColors
        {
            public Color FaceColor;
            public Color GlowColor;
        }

        /// <summary>
        /// 🦾 ММО-КАНОН: Возвращает LDR-подложку и HDR-неон (Glow) строго по школе магии
        /// </summary>
        public static AuraColors GetColorByElement(string element)
        {
            Color baseColor;
            float faceAlpha = 0.27f;

            switch (element?.ToLower())
            {
                case "fire":
                    baseColor = new Color(255f / 255f, 158f / 255f, 100f / 255f); // var(--osaka-orange)
                    break;
                case "frost":
                    baseColor = new Color(122f / 255f, 162f / 255f, 247f / 255f); // var(--osaka-blue)
                    break;
                case "arcane":
                    baseColor = new Color(125f / 255f, 207f / 255f, 255f / 255f); // var(--osaka-cyan)
                    break;
                case "nature":
                    baseColor = new Color(158f / 255f, 206f / 255f, 106f / 255f); // var(--osaka-green)
                    break;
                case "shadow":
                    baseColor = new Color(187f / 255f, 154f / 255f, 247f / 255f); // var(--osaka-purple)
                    break;
                case "holy":
                    baseColor = new Color(224f / 255f, 175f / 255f, 104f / 255f); // var(--osaka-yellow)
                    break;
                case "physical":
                    baseColor = new Color(247f / 255f, 118f / 255f, 142f / 255f); // var(--osaka-red)
                    faceAlpha = 0.35f; 
                    break;
                default:
                    baseColor = new Color(169f / 255f, 178f / 255f, 211f / 255f); // var(--osaka-text-dim)
                    faceAlpha = 0.25f;
                    break;
            }

            // Шёлково юзаем твой собственный метод расширения SetAlpha!
            Color ldrFace = baseColor.SetAlpha(faceAlpha);
            
            Color hdrGlow = new Color(
                baseColor.r * GlowIntensity, 
                baseColor.g * GlowIntensity, 
                baseColor.b * GlowIntensity, 
                1.0f
            );

            return new AuraColors { FaceColor = ldrFace, GlowColor = hdrGlow };
        }

    }
}

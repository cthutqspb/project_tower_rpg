using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    public static class UIUtils
    {
        /// <summary>
        /// Универсальное зажатие любого UI-элемента в границах экрана.
        /// Полный аналог твоего clamp_to_screen из Defold, но на рельсах Flexbox.
        /// </summary>
        public static Vector2 ClampToScreen(VisualElement element, Vector2 targetPos)
        {
            if (element == null || element.panel == null) return targetPos;

            // 1. Получаем физические размеры перетаскиваемого фрейма
            float elementWidth = float.IsNaN(element.layout.width) ? 0f : element.layout.width;
            float elementHeight = float.IsNaN(element.layout.height) ? 0f : element.layout.height;

            // 2. Получаем чистые, адаптированные под разрешение монитора границы экрана
            float screenWidth = element.panel.visualTree.layout.width;
            float screenHeight = element.panel.visualTree.layout.height;

            // 3. Вычисляем заклампленные координаты (не даем элементу вылететь за борта)
            float clampedLeft = Mathf.Clamp(targetPos.x, 0f, screenWidth - elementWidth);
            float clampedTop = Mathf.Clamp(targetPos.y, 0f, screenHeight - elementHeight);

            return new Vector2(clampedLeft, clampedTop);
        }
    }
}


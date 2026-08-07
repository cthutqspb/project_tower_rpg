using UnityEngine;

namespace ProjectTowerRpg.Core.UI
{
    // Четкие геймплейные домены без путаницы с ECS-сущностями
    public enum TooltipDomain : byte
    {
        INTERFACE, // Вместо gui_entity
        WORLD      // Вместо world_entity
    }

    // Нейтральные виды контента
    public enum TooltipKind : byte
    {
        ITEM,
        ABILITY,
        AURA,
        UNIT,
        OBJECT
    }

    public class TooltipSessionData
    {
        public TooltipDomain Domain; // Изменено: быстрый enum
        public TooltipKind Kind;     // Изменено: быстрый enum
        public object Info;          // Чистые данные конфигурации
        public object Context;       // Дополнительные метаданные
    }

    public static class TooltipManager
    {
        private static TooltipSessionData _currentTooltip;

        public static float MouseX { get; private set; }
        public static float MouseY { get; private set; }

        public static void Show(TooltipDomain domain, TooltipKind kind, object info, object context = null)
        {
            _currentTooltip = new TooltipSessionData
            {
                Domain = domain,
                Kind = kind,
                Info = info,
                Context = context
            };
        }

        public static void HideWorldTooltips()
        {
            if (_currentTooltip != null && _currentTooltip.Domain == TooltipDomain.WORLD)
            {
                _currentTooltip = null;
            }
        }

        public static void HideGuiTooltips()
        {
            if (_currentTooltip != null && _currentTooltip.Domain == TooltipDomain.INTERFACE)
            {
                _currentTooltip = null;
            }
        }

        public static TooltipSessionData GetCurrent() => _currentTooltip;

        public static void UpdateMouse(float x, float y)
        {
            MouseX = x;
            MouseY = y;
        }

        public static void Clear()
        {
            _currentTooltip = null;
        }
    }
}


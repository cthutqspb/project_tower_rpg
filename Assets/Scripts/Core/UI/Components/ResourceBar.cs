using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components; // Чтобы дотянуться до нашего ResourceType enum

namespace ProjectTowerRpg.Core.UI.Components
{
    public class ResourceBar
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;

        /// <summary>
        /// Инициализация компонента полоски ресурса (Аналог function M:init)
        /// </summary>
        public ResourceBar(VisualElement barRoot)
        {
            _root = barRoot;
            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;
            _fill.AddToClassList("smooth");
        }

        /// <summary>
        /// Покадрово перекрасить и плавно сжать полоску ЛЮБОГО ресурса вселенной Meadows
        /// (Аналог function M:update_resource)
        /// </summary>
        public void UpdateResource(ResourceType resourceType, float percentage)
        {
            // Си-защита диапазона (как твоя math.max / math.min)
            percentage = Mathf.Clamp01(percentage);

            // Плавно сжимаем/расширяем полоску встроенными силами UI Toolkit Transitions
            _fill.style.width = Length.Percent(percentage * 100f);

            // =========================================================================
            // 🎨 WOW-ТАБЛИЦА СТИЛЕЙ И КЛАССОВЫХ ЦВЕТОВ (ПЕРЕНЕСЕНА КОПЕЙКА В КОПЕЙКУ)
            // =========================================================================
            // Готовим девственные ААА-векторы цветов для каждого типа энергии:
            Color colorMana    = new Color(0.00f, 0.27f, 0.92f, 1.0f); // Твоя благородная синяя мана магов
            Color colorEnergy  = new Color(1.00f, 0.85f, 0.00f, 1.0f); // Чистокровный разбойничий жёлтый цвет
            Color colorRage    = new Color(0.85f, 0.00f, 0.00f, 1.0f); // Свирепый красный цвет воинской ярости

            Color finalColor = colorMana; // Фоллбек на ману, если тип None или сбоит

            if (resourceType == ResourceType.Energy)
            {
                finalColor = colorEnergy;
            }
            else if (resourceType == ResourceType.Rage)
            {
                finalColor = colorRage;
            }
            else if (resourceType == ResourceType.Mana)
            {
                // 🧱 СИ-ЗАЩИТА И ЗАТЕМНЕНИЕ МАНЫ ПРИ ОПУСТОШЕНИИ (Твой оригинальный градиент):
                if (percentage < 0.3f)
                {
                    finalColor = new Color(0.27f, 0.72f, 0.92f, 1.0f); // Светло-синий/бледный при сухом пуле
                }
                else if (percentage < 0.7f)
                {
                    finalColor = new Color(0.00f, 0.50f, 1.00f, 1.0f); // Переходный лазурный
                }
            }

            // Нагло и реактивно красим пиксели фона заливки!
            _fill.style.backgroundColor = finalColor;
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}


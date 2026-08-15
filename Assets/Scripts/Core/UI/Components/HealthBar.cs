using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class HealthBar
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;

        /// <summary>
        /// Инициализация компонента полоски здоровья (Аналог function M:init)
        /// </summary>
        /// <param name="barRoot">Элемент с именем "progress-bar-root" из UXML</param>
        public HealthBar(VisualElement barRoot)
        {
            _root = barRoot;
            
            // Ищем внутри рамки узел заливки по имени "fill"
            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;

            // Сбрасываем в дефолт (100% ХП)
            UpdateHealth(1.0f);
        }

        /// <summary>
        /// Плавно обновить визуальное отображение ХП (Аналог function M:update_health)
        /// </summary>
        /// <param name="percentage">Процент здоровья от 0.0f до 1.0f</param>
        public void UpdateHealth(float percentage)
        {
            // Си-защита диапазона (как твоя math.max / math.min)
            percentage = Mathf.Clamp01(percentage);

            // Реактивно меняем ширину через нативные проценты UI Toolkit.
            // Из-за USS-свойств transition-property, Unity сама плавно ужмет полоску!
            _fill.style.width = Length.Percent(percentage * 100f);

            // ДИНАМИЧЕСКИЙ СВЕТОФОР (Зелёный -> Жёлтый -> Красный)
            Color finalColor = Color.green;

            if (percentage < 0.3f)
            {
                finalColor = Color.red;
            }
            else if (percentage < 0.6f)
            {
                // Желтый в Unity по умолчанию слишком яркий, 
                // можно использовать кастомный красивый WoW-желтый: new Color(1f, 0.85f, 0f, 1f)
                finalColor = Color.yellow; 
            }

            _fill.style.backgroundColor = finalColor;
        }

        /// <summary>
        /// Управление видимостью полоски (Аналог function M:set_visible)
        /// </summary>
        public void SetVisible(bool visible)
        {
            // Flex — аналог gui.set_enabled(true), None — убирает из верстки полностью
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}


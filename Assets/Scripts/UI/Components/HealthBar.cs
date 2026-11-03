using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Colors;

namespace ProjectTowerRpg.UI.Components
{
    public class HealthBar
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;
        private IVisualElementScheduledItem _pulseTask; // Ссылка на задачу пульсации

        public HealthBar(VisualElement barRoot)
        {
            _root = barRoot;
            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;
            
            // Важно: если используете классы в USS, убедитесь, что имя совпадает. 
            // В вашем USS написано .health-fill, а здесь добавлялся .smooth.
            _fill.AddToClassList("health-fill"); 

            // Настраиваем задачу пульсации, которая будет работать каждый кадр (или каждые 10-20 мс)
            // Она изначально на паузе
            _pulseTask = _fill.schedule.Execute(AnimatePulse).Every(16); // ~60 FPS
            _pulseTask.Pause();

            UpdateHealth(1.0f);
        }

        public void UpdateHealth(float percentage)
        {
            percentage = Mathf.Clamp01(percentage);
            _fill.style.width = Length.Percent(percentage * 100f);

            // Честный градиент: Зеленый -> Желтый -> Красный
            Color finalColor;
            if (percentage > 0.5f)
            {
                float t = (percentage - 0.5f) * 2f;
                finalColor = Color.Lerp(SolarizedOsakaNight.Yellow, SolarizedOsakaNight.Green, t);
            }
            else
            {
                float t = percentage * 2f;
                finalColor = Color.Lerp(SolarizedOsakaNight.Red, SolarizedOsakaNight.Yellow, t);
            }
            
            _fill.style.backgroundColor = finalColor;

            // Контроль пульсации при критическом уровне (< 20%)
            if (percentage < 0.2f)
            {
                _pulseTask.Resume();
            }
            else
            {
                _pulseTask.Pause();
                _fill.style.opacity = 1f; // Сбрасываем прозрачность в дефолт
            }
        }

        private void AnimatePulse()
        {
            // Используем время Unity для создания плавной синусоиды
            // 6f — это скорость пульсации. Чем больше, тем быстрее.
            float minOpacity = 0.4f;
            float maxOpacity = 1.0f;
            
            // Переводим синусоиду от -1..1 в диапазон 0..1
            float pulse = (Mathf.Sin(Time.time * 7f) + 1f) / 2f; 
            
            // Интерполируем прозрачность полоски
            _fill.style.opacity = Mathf.Lerp(minOpacity, maxOpacity, pulse);
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Ставим на паузу пульсацию, если бар скрыт, чтобы не тратить ресурсы
            if (!visible) _pulseTask.Pause();
            else if (_fill.style.width.value.value < 20f) _pulseTask.Resume(); 
        }
    }
}


using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class CastBar
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;
        
        // Динамические текстовые Си-ноды оверлея
        private Label _abilityNameLabel;
        private Label _timerLabel;

        /// <summary>
        /// Инициализация компонента полоски каста на базе твоего универсального ProgressBar!
        /// </summary>
        /// <param name="barRoot">Элемент с именем "progress-bar-root" из UXML</param>
        public CastBar(VisualElement barRoot)
        {
            _root = barRoot;
            if (_root == null)
            {
                Debug.LogError("[CastBar] Передан пустой barRoot!");
                return;
            }

            // Ищем внутри рамки узел заливки по твоему имени "fill"
            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;

            // 🦾 ДИНАМИЧЕСКИЙ ОВЕРЛЕЙ ТЕКСТA (WoW-Канон):
            // Чтобы не ломать твой чистый ProgressBar.uxml, мы рождаем текстовый слой прямо в ОЗУ!
            var textOverlay = new VisualElement();
            textOverlay.name = "cast-text-overlay";
            textOverlay.style.position = Position.Absolute;
            textOverlay.style.width = Length.Percent(100f);
            textOverlay.style.height = Length.Percent(100f);
            textOverlay.style.flexDirection = FlexDirection.Row;
            textOverlay.style.justifyContent = Justify.SpaceBetween;
            textOverlay.style.alignItems = Align.Center;
            textOverlay.style.paddingLeft = 8;
            textOverlay.style.paddingRight = 8;
            
            // Наглухо отключаем реакцию на мышь, чтобы текст не мешал драгу и кликам под ним
            textOverlay.pickingMode = PickingMode.Ignore; 

            _abilityNameLabel = new Label("Unknown Spell");
            _abilityNameLabel.style.color = Color.white;
            _abilityNameLabel.style.fontSize = 11;
            _abilityNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _abilityNameLabel.pickingMode = PickingMode.Ignore;

            _timerLabel = new Label("0.0s / 0.0s");
            _timerLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Светло-серый
            _timerLabel.style.fontSize = 11;
            _timerLabel.pickingMode = PickingMode.Ignore;

            textOverlay.Add(_abilityNameLabel);
            textOverlay.Add(_timerLabel);
            
            // Вживляем оверлей в корень рамки
            _root.Add(textOverlay);

            // Изначально кастбар полностью тушим с экрана, пока нет активного каста
            SetVisible(false);
        }

        /// <summary>
        /// Реактивный ММО-апдейт полоски каста, вызываемый покадрово из UIPullSystem
        /// </summary>
        public void UpdateCast(bool isActive, string abilityId, float progress, float duration, bool isChanneling)
        {
            if (!isActive || duration <= 0f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            // 1. Выводим локализованное имя заклинания по ключу
            if (_abilityNameLabel != null)
            {
                _abilityNameLabel.text = LocalizationManager.Get($"spell_{abilityId.ToLower()}");
            }

            // 2. Рассчитываем процент заполнения Си-полоски
            float percentage = Mathf.Clamp01(progress / duration);
            
            // Канон WoW: Потоковое заклинание (Channeling) сочно убывает справа налево!
            if (isChanneling)
            {
                percentage = 1f - percentage;
            }

            if (_fill != null)
            {
                _fill.style.width = Length.Percent(percentage * 100f);

                // Нагло красим заливку в благородный каноничный WoW-золотой цвет каста
                // (При потоковом заклинании можно перекрашивать в WoW-зеленый/бирюзовый, пока держим один)
                _fill.style.backgroundColor = new Color(1f, 0.70f, 0.0f, 1f); 
            }

            // 3. Выводим цифры тикающего таймера кадра ("1.4s / 1.7s")
            if (_timerLabel != null)
            {
                _timerLabel.text = $"{progress:F1}s / {duration:F1}s";
            }
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}


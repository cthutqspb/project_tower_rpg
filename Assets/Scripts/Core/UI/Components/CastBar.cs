using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class CastBar : IEcsUiComponentReceiver<CastComponent>
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;
        private Label _abilityNameLabel;
        private Label _timerLabel;

        private float _lastProgress = -1f;

        public CastBar(VisualElement barRoot)
        {
            _root = barRoot;
            if (_root == null) return;

            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;

            var textOverlay = new VisualElement();
            textOverlay.AddToClassList("cast-bar__text-overlay");
            textOverlay.pickingMode = PickingMode.Ignore;

            _abilityNameLabel = new Label("");
            _abilityNameLabel.AddToClassList("cast-bar__ability-name");
            _abilityNameLabel.pickingMode = PickingMode.Ignore;

            _timerLabel = new Label("");
            _timerLabel.AddToClassList("cast-bar__timer");
            _timerLabel.pickingMode = PickingMode.Ignore;

            textOverlay.Add(_abilityNameLabel);
            textOverlay.Add(_timerLabel);
            _root.Add(textOverlay);

            _fill.style.transitionProperty = StyleKeyword.Null;
            _fill.style.transitionDuration = StyleKeyword.Null;
            _fill.style.transitionTimingFunction = StyleKeyword.Null;

            SetVisible(false);
        }

        public void UpdateFromComponent(ref CastComponent cast)
        {
            if (!cast.IsActive || cast.CastTime <= 0f)
            {
                SetVisible(false);
                _lastProgress = -1f;
                return;
            }

            SetVisible(true);

            if (_abilityNameLabel != null)
            {
                _abilityNameLabel.text = LocalizationManager.Get($"spell_{cast.AbilityId.ToString().ToLower()}");
            }

            // ✅ ПРЯМОЕ ОБНОВЛЕНИЕ ПО ПРОГРЕССУ ИЗ ECS (без независимого таймера!)
            float percentage = Mathf.Clamp01(cast.Progress / cast.CastTime);
            if (cast.IsChanneling)
            {
                percentage = 1f - percentage;
            }

            // Обновляем только если изменилось
            if (!Mathf.Approximately(percentage, _lastProgress))
            {
                _fill.style.width = Length.Percent(percentage * 100f);
                _fill.style.backgroundColor = new Color(1f, 0.70f, 0.0f, 1f);
                _lastProgress = percentage;
            }

            if (_timerLabel != null)
            {
                _timerLabel.text = $"{cast.Progress:F1}s / {cast.CastTime:F1}s";
            }
        }

        private void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                _lastProgress = -1f;
                _fill.style.width = Length.Percent(0f);
            }
        }
    }
}

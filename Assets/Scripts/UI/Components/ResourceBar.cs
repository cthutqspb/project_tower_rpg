using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Colors;

namespace ProjectTowerRpg.UI.Components
{
    public class ResourceBar
    {
        private readonly VisualElement _root;
        private readonly VisualElement _fill;

        public ResourceBar(VisualElement barRoot)
        {
            _root = barRoot;
            _fill = barRoot.Q<VisualElement>("fill") ?? barRoot;
            _fill.AddToClassList("smooth");
        }

        public void UpdateResource(ResourceType resourceType, float percentage)
        {
            percentage = Mathf.Clamp01(percentage);
            _fill.style.width = Length.Percent(percentage * 100f);

            Color finalColor;

            switch (resourceType)
            {
                case ResourceType.Energy:
                    // ⚡ Энергия — всегда жёлтая, без градиента
                    finalColor = SolarizedOsakaNight.Yellow;
                    break;

                case ResourceType.Rage:
                    // 🔥 Ярость — всегда оранжевая, без градиента
                    finalColor = SolarizedOsakaNight.Orange;
                    break;

                case ResourceType.Mana:
                default:
                    // 💧 Мана — честный градиент от SurfaceLight (0%) к Blue (100%)
                    finalColor = Color.Lerp(SolarizedOsakaNight.SurfaceLight, SolarizedOsakaNight.Blue, percentage);
                    break;
            }

            _fill.style.backgroundColor = finalColor;
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

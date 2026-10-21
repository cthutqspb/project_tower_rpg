using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class NameplateElement : VisualElement
    {
        private Label _nameLabel;
        private Label _markLabel;

        private NameplateRelation _lastRelation = (NameplateRelation)255;
        private NameplateMark _lastMark = (NameplateMark)255;

        private const string QuestMarkGlyph = "󱈸";

        public NameplateElement()
        {
            AddToClassList("nameplate");
            pickingMode = PickingMode.Ignore;

            _markLabel = new Label(QuestMarkGlyph);
            _markLabel.AddToClassList("nameplate__mark");
            _markLabel.style.display = DisplayStyle.None;
            Add(_markLabel);

            _nameLabel = new Label();
            _nameLabel.AddToClassList("nameplate__name");
            Add(_nameLabel);
        }

        public void SetName(string name) => _nameLabel.text = name;

        public void SetPosition(float x, float y) { style.left = x; style.top = y; }
        public void SetDepth(int zIndex) => style.zIndex = zIndex;
        public void SetActive(bool active) => style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetRelation(NameplateRelation relation)
        {
            if (_lastRelation == relation) return;
            _lastRelation = relation;

            _nameLabel.RemoveFromClassList("nameplate__name--hostile");
            _nameLabel.RemoveFromClassList("nameplate__name--neutral");
            _nameLabel.RemoveFromClassList("nameplate__name--friendly");
            _nameLabel.RemoveFromClassList("nameplate__name--player");

            switch (relation)
            {
                case NameplateRelation.Hostile:  _nameLabel.AddToClassList("nameplate__name--hostile"); break;
                case NameplateRelation.Neutral:  _nameLabel.AddToClassList("nameplate__name--neutral"); break;
                case NameplateRelation.Friendly: _nameLabel.AddToClassList("nameplate__name--friendly"); break;
                case NameplateRelation.Player:   _nameLabel.AddToClassList("nameplate__name--player"); break;
            }
        }

        public void SetMark(NameplateMark mark)
        {
            if (_lastMark == mark) return;
            _lastMark = mark;

            _markLabel.RemoveFromClassList("nameplate__mark--quest");

            switch (mark)
            {
                case NameplateMark.Quest:
                    _markLabel.AddToClassList("nameplate__mark--quest");
                    _markLabel.style.display = DisplayStyle.Flex;
                    break;

                case NameplateMark.None:
                default:
                    _markLabel.style.display = DisplayStyle.None;
                    break;
            }
        }
    }
}

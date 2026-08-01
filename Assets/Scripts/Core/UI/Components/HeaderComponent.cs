using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class HeaderComponent : VisualElement
    {   
        private Label _titleLabel;
        private Button _closeButton;
        
        public string Title
        {
            get => _titleLabel?.text ?? "";
            set { if (_titleLabel != null) _titleLabel.text = value; }
        }
        
        public event System.Action OnClose;


        public HeaderComponent(bool hasCloseButton = true)
        {
            this.AddToClassList("header-component");
            
            _titleLabel = new Label();
            _titleLabel.AddToClassList("header-component__label");
            _titleLabel.text = "Заголовок";
            Add(_titleLabel);
            
            if (hasCloseButton)
            {
                _closeButton = new Button();
                _closeButton.AddToClassList("header-component__close-btn");
                _closeButton.text = "✕";
                _closeButton.RegisterCallback<ClickEvent>(_ => OnClose?.Invoke());
                Add(_closeButton);
            }
        }
    }
}

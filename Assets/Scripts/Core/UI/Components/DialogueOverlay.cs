using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Localization;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class DialogueOverlay : MonoBehaviour,
                                    IEcsUiComponentReceiver<DialogueStateComponent>,
                                    IEcsUiBufferReceiver<DialogueOptionElement>
    {
        private VisualElement _root;
        private Label _titleLabel;
        private Label _npcTextLabel;
        private VisualElement _optionsContainer;

        private Entity _boundEntity = Entity.Null;
        public Entity BoundEntity => _boundEntity;

        // Цвета в формате HEX для Rich Text
        private const string ColorNumber  = "#7dcfff"; // --osaka-cyan
        private const string ColorNormal  = "#c0caf5"; // --osaka-text
        private const string ColorVisited = "#565f89"; // --osaka-text-dim
        private const string ColorHover   = "#e0af68"; // --osaka-yellow
        private const string ColorSuffix  = "#e0af68"; // --osaka-yellow

        public void BindToEntity(Entity entity)
        {
            _boundEntity = entity;
        }

        public void Initialize(VisualElement rootElement)
        {
            _root = rootElement.Q<VisualElement>("root");
            if (_root == null) return;

            _titleLabel = _root.Q<Label>("title");
            _npcTextLabel = _root.Q<Label>("npc-text");
            _optionsContainer = _root.Q<VisualElement>("options");

            _root.style.display = DisplayStyle.None;
        }

        public void UpdateFromComponent(ref DialogueStateComponent state)
        {
            if (!state.IsActive)
            {
                if (_root != null) _root.style.display = DisplayStyle.None;
                return;
            }

            if (_root != null) _root.style.display = DisplayStyle.Flex;

            if (_titleLabel != null)
                _titleLabel.text = LocalizationManager.Get(state.SpeakerNameKey).ToUpper();

            if (_npcTextLabel != null)
                _npcTextLabel.text = LocalizationManager.Get(state.TextKey);
        }

        public void UpdateFromBuffer(
            DynamicBuffer<DialogueOptionElement> buffer,
            bool isOnlyValidation = false,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer<ActiveCooldownElement> cooldowns = default
        )
        {
            if (_optionsContainer == null) return;
            _optionsContainer.Clear();

            for (int i = 0; i < buffer.Length; i++)
            {
                var opt = buffer[i];
                string text = LocalizationManager.Get(opt.TextKey);
                string suffix = "";

                if (!opt.SuffixKey.IsEmpty)
                {
                    suffix = $" <color={ColorSuffix}>{LocalizationManager.Get(opt.SuffixKey)}</color>";
                }

                string baseColor = opt.IsVisited ? ColorVisited : ColorNormal;

                // 🎯 Число — отдельным цветом (osaka-cyan), текст — основным, суффикс — золотым
                string normalText = $"<color={ColorNumber}>{i + 1}.</color> <color={baseColor}>{text}</color>{suffix}";
                string hoverText  = $"<color={ColorNumber}>{i + 1}.</color> <color={ColorHover}>{text}</color>{suffix}";

                Button btn = new Button();
                btn.enableRichText = true;
                btn.text = normalText;
                btn.AddToClassList("dialogue-overlay__option-button");

                // 🎯 Меняем текст при наведении (CSS :hover перебьет <color>, поэтому делаем через C#)
                btn.RegisterCallback<PointerEnterEvent>(_ => btn.text = hoverText);
                btn.RegisterCallback<PointerLeaveEvent>(_ => btn.text = normalText);

                int capturedIndex = opt.OptionIndex;
                btn.clicked += () => OnOptionClicked(capturedIndex);

                _optionsContainer.Add(btn);
            }
        }

        private void OnOptionClicked(int optionIndex)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _boundEntity == Entity.Null) return;

            var ecbSystem = world.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            var req = ecb.CreateEntity();
            ecb.AddComponent(req, new DialogueChooseOptionRequest
            {
                Unit = _boundEntity,
                OptionIndex = optionIndex
            });
        }
    }
}

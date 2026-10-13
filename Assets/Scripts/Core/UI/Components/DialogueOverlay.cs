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

        // ============================================================
        // Реактивный приём состояния диалога
        // ============================================================
        public void UpdateFromComponent(ref DialogueStateComponent state)
        {
            if (!state.IsActive)
            {
                if (_root != null) _root.style.display = DisplayStyle.None;
                return;
            }

            if (_root != null) _root.style.display = DisplayStyle.Flex;

            if (_titleLabel != null)
                _titleLabel.text = LocalizationManager.Get(state.SpeakerNameKey.ToString()).ToUpper();

            if (_npcTextLabel != null)
                _npcTextLabel.text = LocalizationManager.Get(state.TextKey.ToString());
        }

        // ============================================================
        // Реактивный приём буфера опций
        // ============================================================
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
                string text = LocalizationManager.Get(opt.TextKey.ToString());
                string suffix = "";

                if (!opt.SuffixKey.IsEmpty)
                {
                    string suffixLocalized = LocalizationManager.Get(opt.SuffixKey.ToString());
                    if (opt.SuffixKey.ToString() == "ui_dialogue_action_quest")
                        suffix = $" <color=#e0af68>{suffixLocalized}</color>";
                    else
                        suffix = $" {suffixLocalized}";
                }

                Button btn = new Button();
                btn.AddToClassList("dialogue-overlay__option-button");
                if (opt.IsVisited)
                    btn.AddToClassList("visited");

                btn.text = $"> {text}{suffix}";

                int capturedIndex = opt.OptionIndex;
                btn.clicked += () => OnOptionClicked(capturedIndex);

                _optionsContainer.Add(btn);
            }
        }

        private void OnOptionClicked(int optionIndex)
        {
            var ecb = World.DefaultGameObjectInjectionWorld
                .GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();

            var req = ecb.CreateEntity();
            ecb.AddComponent(req, new DialogueChooseOptionRequest
            {
                Player = _boundEntity,
                OptionIndex = optionIndex
            });
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class QuestEndPopup : UIWindow, IPayloadReceiver
    {
        public override WindowType Type => WindowType.QuestEnd;

        private VisualElement _choiceContainer;
        private VisualElement _guaranteedContainer;
        private Label _choiceHeader;
        private Label _guaranteedHeader;
        private Label _questNameLabel;
        private Button _completeBtn;

        private Entity _unitEntity = Entity.Null;
        private string _questId;
        private int _selectedChoiceIndex = -1;
        private readonly List<SlotPreviewElement> _choicePreviews = new();

        public override Entity BoundEntity => _unitEntity;
        public override bool CanCloseByEscape => false;

        public override void Setup(Entity entity)
        {
            _unitEntity = entity;
            Open();
        }

        public void ReceivePayload(object payload)
        {
            if (payload is string questId)
            {
                _questId = questId;
                BuildUI();
                CenterOnScreen();
            }
        }

        private void CenterOnScreen()
        {
            if (_root == null) return;

            _root.schedule.Execute(() =>
            {
                var w = _root.resolvedStyle.width;
                var h = _root.resolvedStyle.height;
                if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

                _root.style.left = new Length(50, LengthUnit.Percent);
                _root.style.top = new Length(50, LengthUnit.Percent);
                _root.style.marginLeft = -w / 2;
                _root.style.marginTop = -h / 2;
            }).ExecuteLater(1);
        }

        protected override void OnWindowBuilt(VisualElement root)
        {
            var header = root.Q<VisualElement>("header");
            _questNameLabel = root.Q<Label>("quest-name");
            _choiceHeader = root.Q<Label>("choice-header");
            _guaranteedHeader = root.Q<Label>("guaranteed-header");
            _choiceContainer = root.Q<VisualElement>("rewards-choice");
            _guaranteedContainer = root.Q<VisualElement>("rewards-guaranteed");
            _completeBtn = root.Q<Button>("complete-btn");

            if (_completeBtn != null)
                _completeBtn.clicked += OnCompleteClicked;

            if (header != null && root != null)
            {
                var drag = new DragManipulator(header, _window, DragMode.UIElement);
                header.AddManipulator(drag);
            }
        }

        private void BuildUI()
        {
            if (string.IsNullOrEmpty(_questId)) return;

            var config = QuestsDatabase.GetQuest(_questId);
            if (config == null)
            {
                Debug.LogError($"[QuestEndPopup] Нет конфига для '{_questId}'");
                return;
            }

            if (_questNameLabel != null)
                _questNameLabel.text = LocalizationManager.Get(config.title_key);

            _choiceContainer?.Clear();
            _guaranteedContainer?.Clear();
            _choicePreviews.Clear();
            _selectedChoiceIndex = -1;

            bool hasChoice = false;

            if (config.rewards != null)
            {
                for (int i = 0; i < config.rewards.Count; i++)
                {
                    var reward = config.rewards[i];

                    if (reward.is_choice)
                    {
                        hasChoice = true;
                        var preview = CreateItemPreview(reward, i);
                        _choiceContainer?.Add(preview);
                        _choicePreviews.Add(preview);
                    }
                    else
                    {
                        var el = CreateGuaranteedElement(reward);
                        _guaranteedContainer?.Add(el);
                    }
                }
            }

            // Скрываем пустые блоки
            if (_choiceHeader != null)
                _choiceHeader.style.display = hasChoice ? DisplayStyle.Flex : DisplayStyle.None;
            if (_choiceContainer != null)
                _choiceContainer.style.display = hasChoice ? DisplayStyle.Flex : DisplayStyle.None;

            bool hasGuaranteed = _guaranteedContainer?.childCount > 0;
            if (_guaranteedHeader != null)
                _guaranteedHeader.style.display = hasGuaranteed ? DisplayStyle.Flex : DisplayStyle.None;
            if (_guaranteedContainer != null)
                _guaranteedContainer.style.display = hasGuaranteed ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateCompleteButtonState(hasChoice && _selectedChoiceIndex < 0);
        }

        private SlotPreviewElement CreateItemPreview(QuestReward reward, int index)
        {
            var preview = new SlotPreviewElement();
            preview.SetItem(reward.id, reward.amount);

            if (reward.is_choice)
            {
                preview.SetChoice(true);
                int capturedIndex = index;
                preview.OnClicked += (p) => SelectChoice(capturedIndex, p);
            }

            return preview;
        }

        private VisualElement CreateGuaranteedElement(QuestReward reward)
        {
            if (reward.kind == "item")
            {
                var preview = new SlotPreviewElement();
                preview.SetItem(reward.id, reward.amount);
                return preview;
            }

            // Не-предмет: опыт, валюта, способность
            var row = new VisualElement();
            row.AddToClassList("quest-end__reward-item");

            var icon = new Label(GetNonItemIcon(reward));
            icon.AddToClassList("quest-end__reward-icon");
            row.Add(icon);

            var name = new Label(GetNonItemName(reward));
            name.AddToClassList("quest-end__reward-name");
            row.Add(name);

            return row;
        }

        private string GetNonItemIcon(QuestReward reward)
        {
            switch (reward.kind)
            {
                case "experience": return "★";
                case "currency": return "¤";
                case "ability": return "✦";
                default: return "?";
            }
        }

        private string GetNonItemName(QuestReward reward)
        {
            switch (reward.kind)
            {
                case "experience": return $"Опыт +{reward.amount}";
                case "currency": return $"{reward.id} +{reward.amount}";
                case "ability": return $"Способность: {reward.id}";
                default: return reward.id ?? reward.kind;
            }
        }

        private void SelectChoice(int index, SlotPreviewElement selectedPreview)
        {
            _selectedChoiceIndex = index;

            foreach (var p in _choicePreviews)
                p.SetSelected(false);

            selectedPreview.SetSelected(true);
            UpdateCompleteButtonState(false);
        }

        private void UpdateCompleteButtonState(bool disabled)
        {
            if (_completeBtn == null) return;

            if (disabled)
                _completeBtn.AddToClassList("quest-end__btn--disabled");
            else
                _completeBtn.RemoveFromClassList("quest-end__btn--disabled");

            _completeBtn.SetEnabled(!disabled);
        }

        private void OnCompleteClicked()
        {
            if (string.IsNullOrEmpty(_questId) || _unitEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var reqEntity = em.CreateEntity();
            em.AddComponentData(reqEntity, new QuestRewardClaimRequest
            {
                Unit = _unitEntity,
                QuestId = _questId,
                ChosenRewardIndex = _selectedChoiceIndex
            });

            Debug.Log($"[QuestEndPopup] Запрос на награду: '{_questId}', choice={_selectedChoiceIndex}");
            Close();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class QuestJournal : VisualElement,
                                IEcsUiBufferReceiver<QuestComponent>,
                                IEcsUiBufferReceiver<QuestObjectiveComponent>
    {
        private Entity _unitEntity = Entity.Null;
        public Entity BoundEntity => _unitEntity;

        private ScrollView _questList;
        private ScrollView _questDetails;
        private Label _detailTitle;
        private Label _detailDescription;
        private VisualElement _detailObjectives;
        private VisualElement _detailRewardBlock;
        
        private Label _choiceHeader;
        private Label _guaranteedHeader;
        private VisualElement _choiceContainer;
        private VisualElement _guaranteedContainer;
        
        private Toggle _hideCompletedToggle;

        private bool _hideCompleted = true;

        // Последний выбранный (живёт между экземплярами)
        private static string _lastSelectedQuestId = null;

        // Текущий выбранный (в этом экземпляре)
        private string _selectedQuestId = null;

        private readonly List<QuestEntrySnapshot> _quests = new();
        private readonly List<QuestObjectiveSnapshot> _objectives = new();

        private struct QuestEntrySnapshot
        {
            public string QuestId;
            public QuestState State;
            public bool IsTracked;
        }

        private struct QuestObjectiveSnapshot
        {
            public string QuestId;
            public string ObjectiveId;
            public int Current;
            public int Required;
            public bool IsCompleted;
        }

        public QuestJournal(VisualTreeAsset uxml)
        {
            this.AddToClassList("quest-journal");

            if (uxml == null)
            {
                Debug.LogError("[QuestJournal] UXML не передан!");
                return;
            }

            uxml.CloneTree(this);

            _questList = this.Q<ScrollView>("quest-list");
            _questDetails = this.Q<ScrollView>("quest-details");
            _detailTitle = this.Q<Label>("detail-title");
            _detailDescription = this.Q<Label>("detail-description");
            _detailObjectives = this.Q<VisualElement>("detail-objectives");
            _detailRewardBlock = this.Q<VisualElement>("detail-reward-block");
            
            _choiceHeader = this.Q<Label>("choice-header");
            _guaranteedHeader = this.Q<Label>("guaranteed-header");
            _choiceContainer = this.Q<VisualElement>("rewards-choice");
            _guaranteedContainer = this.Q<VisualElement>("rewards-guaranteed");

            _hideCompletedToggle = this.Q<Toggle>("hide-completed-toggle");

            if (_hideCompletedToggle != null)
            {
                _hideCompletedToggle.value = _hideCompleted;
                _hideCompletedToggle.RegisterValueChangedCallback(evt =>
                {
                    _hideCompleted = evt.newValue;
                    RebuildQuestList();
                });
            }

            ClearDetails();
        }

        public void BindToEntity(Entity unitEntity)
        {
            if (_unitEntity != Entity.Null)
                UIRegistry.Unregister(_unitEntity, this);

            _unitEntity = unitEntity;

            if (_unitEntity == Entity.Null) return;

            UIRegistry.Register(_unitEntity, this);

            // Восстанавливаем последний выбранный
            if (string.IsNullOrEmpty(_selectedQuestId) && !string.IsNullOrEmpty(_lastSelectedQuestId))
                _selectedQuestId = _lastSelectedQuestId;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (em.HasBuffer<QuestComponent>(_unitEntity))
                UpdateFromBuffer(em.GetBuffer<QuestComponent>(_unitEntity), false);

            if (em.HasBuffer<QuestObjectiveComponent>(_unitEntity))
                UpdateFromBuffer(em.GetBuffer<QuestObjectiveComponent>(_unitEntity), false);
        }

        public void Unbind()
        {
            if (_unitEntity != Entity.Null)
                UIRegistry.Unregister(_unitEntity, this);
            _unitEntity = Entity.Null;
        }

        public void UpdateFromBuffer(
            DynamicBuffer<QuestComponent> buffer,
            bool isOnlyValidation = false,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer<ActiveCooldownElement> cooldowns = default)
        {
            _quests.Clear();

            for (int i = 0; i < buffer.Length; i++)
            {
                _quests.Add(new QuestEntrySnapshot
                {
                    QuestId = buffer[i].QuestId.ToString(),
                    State = buffer[i].State,
                    IsTracked = buffer[i].IsTracked
                });
            }

            // Если выбранный квест пропал — выбрать первый активный
            bool currentStillExists = !string.IsNullOrEmpty(_selectedQuestId)
                && _quests.Exists(q => q.QuestId == _selectedQuestId && q.State != QuestState.TurnedIn);

            if (!currentStillExists)
            {
                var firstActive = _quests.Find(q => q.State != QuestState.TurnedIn);
                _selectedQuestId = firstActive.QuestId;
            }

            RebuildQuestList();
            RefreshDetails();
        }

        public void UpdateFromBuffer(
            DynamicBuffer<QuestObjectiveComponent> buffer,
            bool isOnlyValidation = false,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer<ActiveCooldownElement> cooldowns = default)
        {
            _objectives.Clear();

            for (int i = 0; i < buffer.Length; i++)
            {
                _objectives.Add(new QuestObjectiveSnapshot
                {
                    QuestId = buffer[i].QuestId.ToString(),
                    ObjectiveId = buffer[i].ObjectiveId.ToString(),
                    Current = buffer[i].Current,
                    Required = buffer[i].Required,
                    IsCompleted = buffer[i].IsCompleted
                });
            }

            RebuildQuestList();
            RefreshDetails();
        }

        private void RebuildQuestList()
        {
            if (_questList == null) return;
            _questList.Clear();

            var active = new List<QuestEntrySnapshot>();
            var completed = new List<QuestEntrySnapshot>();

            foreach (var q in _quests)
            {
                if (q.State == QuestState.Active || q.State == QuestState.Completed)
                    active.Add(q);
                else if (q.State == QuestState.TurnedIn)
                    completed.Add(q);
            }

            if (active.Count > 0)
            {
                _questList.Add(CreateSectionHeader("АКТИВНЫЕ"));
                foreach (var q in active)
                    _questList.Add(CreateQuestButton(q));
            }

            if (completed.Count > 0 && !_hideCompleted)
            {
                _questList.Add(CreateSectionHeader("ВЫПОЛНЕННЫЕ"));
                foreach (var q in completed)
                    _questList.Add(CreateQuestButton(q));
            }

            if (active.Count == 0 && (completed.Count == 0 || _hideCompleted))
            {
                var emptyLabel = new Label("Нет активных заданий.");
                emptyLabel.AddToClassList("quest-journal__objective-mini");
                _questList.Add(emptyLabel);
            }
        }

        private Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.AddToClassList("quest-journal__section-header");
            return header;
        }

        private Button CreateQuestButton(QuestEntrySnapshot quest)
        {
            var btn = new Button();
            btn.AddToClassList("quest-journal__quest-btn");

            if (quest.QuestId == _selectedQuestId)
                btn.AddToClassList("quest-journal__quest-btn--active");

            var config = QuestsDatabase.GetQuest(quest.QuestId);
            string title = config != null
                ? LocalizationManager.Get(config.title_key)
                : quest.QuestId;

            // Строка: чекбокс + название
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.AddToClassList("quest-journal__header-row");

            var trackToggle = new Toggle();
            trackToggle.AddToClassList("quest-journal__track-toggle");
            trackToggle.value = quest.IsTracked;

            string capturedIdForToggle = quest.QuestId;
            trackToggle.RegisterValueChangedCallback(evt =>
            {
                SendTrackQuest(evt.newValue ? capturedIdForToggle : "");
                evt.StopPropagation();
            });

            headerRow.Add(trackToggle);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("quest-journal__quest-name");
            headerRow.Add(titleLabel);

            btn.Add(headerRow);

            foreach (var obj in _objectives)
            {
                if (obj.QuestId != quest.QuestId) continue;

                var objConfig = FindObjectiveConfig(config, obj.ObjectiveId);
                string objText = objConfig != null
                    ? LocalizationManager.Get(objConfig.text_key)
                    : obj.ObjectiveId;

                string prefix = obj.IsCompleted ? "✓" : "•";
                string progress = obj.Required > 1 ? $" [{obj.Current}/{obj.Required}]" : "";

                var objLabel = new Label($"{prefix} {objText}{progress}");
                objLabel.AddToClassList("quest-journal__objective-mini");
                if (obj.IsCompleted) objLabel.AddToClassList("quest-journal__objective-mini--completed");

                btn.Add(objLabel);
            }

            string capturedQuestId = quest.QuestId;
            btn.clicked += () =>
            {
                _selectedQuestId = capturedQuestId;
                _lastSelectedQuestId = capturedQuestId;
                RebuildQuestList();
                RefreshDetails();
            };

            return btn;
        }

        private QuestObjective FindObjectiveConfig(QuestConfig config, string objectiveId)
        {
            if (config?.objectives == null) return null;
            foreach (var o in config.objectives)
                if (o.id == objectiveId) return o;
            return null;
        }

        private void RefreshDetails()
        {
            if (string.IsNullOrEmpty(_selectedQuestId))
            {
                ClearDetails();
                return;
            }

            var config = QuestsDatabase.GetQuest(_selectedQuestId);
            if (config == null)
            {
                ClearDetails();
                return;
            }

            if (_detailTitle != null)
                _detailTitle.text = LocalizationManager.Get(config.title_key).ToUpper();

            if (_detailDescription != null)
                _detailDescription.text = LocalizationManager.Get(config.desc_key);

            if (_detailObjectives != null)
            {
                _detailObjectives.Clear();

                foreach (var obj in _objectives)
                {
                    if (obj.QuestId != _selectedQuestId) continue;

                    var objConfig = FindObjectiveConfig(config, obj.ObjectiveId);
                    string objText = objConfig != null
                        ? LocalizationManager.Get(objConfig.text_key)
                        : obj.ObjectiveId;

                    string prefix = obj.IsCompleted ? "✓" : "☐";
                    string progress = obj.Required > 1 ? $" ({obj.Current}/{obj.Required})" : "";

                    var label = new Label($"{prefix} {objText}{progress}");
                    label.AddToClassList("quest-journal__objective-detail");
                    if (obj.IsCompleted) label.AddToClassList("quest-journal__objective-detail--completed");

                    _detailObjectives.Add(label);
                }
            }

            BuildRewardBlock(config);
        }

        private void SendTrackQuest(string questId)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity == Entity.Null) return;

            var requestEntity = em.CreateEntity();
            em.AddComponentData(requestEntity, new QuestTrackRequest
            {
                Unit = playerEntity,
                QuestId = questId
            });
        }

        private void BuildRewardBlock(QuestConfig config)
        {
            if (_detailRewardBlock == null || _choiceContainer == null) return;

            bool hasRewards = config.rewards != null && config.rewards.Count > 0;
            if (!hasRewards)
            {
                _detailRewardBlock.style.display = DisplayStyle.None;
                return;
            }

            _detailRewardBlock.style.display = DisplayStyle.Flex;
            _choiceContainer.Clear();
            _guaranteedContainer.Clear();

            var choiceRewards = new List<QuestReward>();
            var guaranteedRewards = new List<QuestReward>();

            foreach (var r in config.rewards)
            {
                if (r.is_choice) choiceRewards.Add(r);
                else guaranteedRewards.Add(r);
            }

            // Настройка видимости заголовков
            _choiceHeader.style.display = choiceRewards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _choiceContainer.style.display = choiceRewards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            _guaranteedHeader.style.display = guaranteedRewards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _guaranteedContainer.style.display = guaranteedRewards.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var r in choiceRewards)
                _choiceContainer.Add(CreateRewardElement(r));

            foreach (var r in guaranteedRewards)
                _guaranteedContainer.Add(CreateRewardElement(r));
        }

        private VisualElement CreateRewardElement(QuestReward reward)
        {
            if (reward.kind == "item")
            {
                var preview = new SlotPreviewElement();
                preview.SetItem(reward.id, reward.amount);
                return preview;
            }

            // Не-предметы (опыт, валюта, способность)
            var row = new VisualElement();
            row.AddToClassList("quest-journal__reward-item");

            string displayName;
            string iconChar;

            switch (reward.kind)
            {
                case "experience":
                    displayName = $"Опыт: {reward.amount}";
                    iconChar = "★";
                    break;
                case "currency":
                    displayName = $"Валюта: {reward.id}";
                    iconChar = "¤";
                    break;
                case "ability":
                    displayName = $"Способность: {reward.id}";
                    iconChar = "✦";
                    break;
                default:
                    displayName = reward.id ?? reward.kind;
                    iconChar = "?";
                    break;
            }

            var icon = new Label(iconChar);
            icon.AddToClassList("quest-journal__reward-icon");
            row.Add(icon);

            var name = new Label(displayName);
            name.AddToClassList("quest-journal__reward-name");
            row.Add(name);

            return row;
        }

        private void ClearDetails()
        {
            if (_detailTitle != null) _detailTitle.text = "";
            if (_detailDescription != null) _detailDescription.text = "";
            if (_detailObjectives != null) _detailObjectives.Clear();
            if (_detailRewardBlock != null) _detailRewardBlock.style.display = DisplayStyle.None;
        }
    }
}

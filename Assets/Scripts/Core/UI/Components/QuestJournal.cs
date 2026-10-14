using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Localization;

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
        private Label _detailRewardHeader;
        private VisualElement _detailRewardItems;
        private Toggle _hideCompletedToggle;

        private bool _hideCompleted = true;
        private string _selectedQuestId = null;

        // Кэш буферов из последнего апдейта, чтобы использовать в рендере
        private readonly List<QuestEntrySnapshot> _quests = new();
        private readonly List<QuestObjectiveSnapshot> _objectives = new();

        private struct QuestEntrySnapshot
        {
            public string QuestId;
            public QuestState State;
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
            _detailRewardHeader = this.Q<Label>("detail-reward-header");
            _detailRewardItems = this.Q<VisualElement>("detail-reward-items");
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

        // ============================================================
        // BindToEntity
        // ============================================================
        public void BindToEntity(Entity unitEntity)
        {
            if (_unitEntity != Entity.Null)
                UIRegistry.Unregister(_unitEntity, this);

            _unitEntity = unitEntity;

            if (_unitEntity == Entity.Null) return;

            UIRegistry.Register(_unitEntity, this);

            // Мгновенный snapshot из ECS
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

        // ============================================================
        // Реактивный приём квестов
        // ============================================================
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
                    State = buffer[i].State
                });
            }

            RebuildQuestList();
            RefreshDetails();
        }

        // ============================================================
        // Реактивный приём целей
        // ============================================================
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

        // ============================================================
        // Сборка списка квестов
        // ============================================================
        private void RebuildQuestList()
        {
            if (_questList == null) return;
            _questList.Clear();

            // Активные
            var active = new List<QuestEntrySnapshot>();
            var completed = new List<QuestEntrySnapshot>();

            foreach (var q in _quests)
            {
                if (q.State == QuestState.Active || q.State == QuestState.ReadyToTurnIn)
                    active.Add(q);
                else if (q.State == QuestState.Completed)
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

            // Название квеста
            var config = QuestsDatabase.GetQuest(quest.QuestId);
            string title = config != null
                ? LocalizationManager.Get(config.title_key)
                : quest.QuestId;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("quest-journal__quest-name");
            btn.Add(titleLabel);

            // Мини-цели
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

        // ============================================================
        // Правая панель — детали выбранного квеста
        // ============================================================
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

            // Цели
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

            // Награда
            BuildRewardBlock(config);
        }

        private void BuildRewardBlock(QuestConfig config)
        {
            if (_detailRewardBlock == null || _detailRewardItems == null) return;

            bool hasRewards = config.rewards != null && config.rewards.Count > 0;

            if (!hasRewards)
            {
                _detailRewardBlock.style.display = DisplayStyle.None;
                return;
            }

            _detailRewardBlock.style.display = DisplayStyle.Flex;
            _detailRewardItems.Clear();

            if (_detailRewardHeader != null)
                _detailRewardHeader.text = LocalizationManager.Get("quest_journal_reward_header");

            foreach (var reward in config.rewards)
            {
                var itemConfig = ItemsDatabase.GetItem(reward.item_id);
                string displayName = itemConfig != null
                    ? LocalizationManager.Get(itemConfig.identity.name_key)
                    : reward.item_id;
                string iconChar = itemConfig?.visuals?.icon_char ?? "?";

                var row = new VisualElement();
                row.AddToClassList("quest-journal__reward-item");

                var icon = new Label(iconChar);
                icon.AddToClassList("quest-journal__reward-icon");
                row.Add(icon);

                var name = new Label(displayName);
                name.AddToClassList("quest-journal__reward-name");
                row.Add(name);

                if (reward.amount > 1)
                {
                    var amount = new Label($"x{reward.amount}");
                    amount.AddToClassList("quest-journal__reward-amount");
                    row.Add(amount);
                }

                _detailRewardItems.Add(row);
            }
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

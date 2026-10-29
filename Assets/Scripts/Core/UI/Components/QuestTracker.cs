using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Quest;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class QuestTracker : VisualElement,
        IEcsUiBufferReceiver<QuestComponent>,
        IEcsUiBufferReceiver<QuestObjectiveComponent>
    {
        private Entity _unitEntity = Entity.Null;
        public Entity BoundEntity => _unitEntity;

        private VisualElement _questContainer;
        private ScrollView _body;
        private Button _collapseBtn;

        private bool _collapsed = false;

        // 🧊 Гвард: не перестраиваем скрытый/неизменный трекер
        private int _lastSnapshotHash = int.MinValue;
        private bool _isShown;
        private bool _dirty;
        private bool _flushScheduled;

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

        public QuestTracker(VisualTreeAsset uxml)
        {
            this.AddToClassList("quest-tracker");

            if (uxml == null)
            {
                Debug.LogError("[QuestTracker] UXML не передан!");
                return;
            }

            uxml.CloneTree(this);

            _questContainer = this.Q<VisualElement>("quest-container");
            _body = this.Q<ScrollView>("body");
            _collapseBtn = this.Q<Button>("collapse-btn");

            if (_collapseBtn != null) _collapseBtn.clicked += OnCollapseClicked;

            Rebuild();
        }

        // ============================================================
        // Bind / Unbind
        // ============================================================
        public void BindToEntity(Entity unitEntity)
        {
            if (_unitEntity != Entity.Null)
                UIRegistry.Unregister(_unitEntity, this);

            _unitEntity = unitEntity;

            if (_unitEntity == Entity.Null) return;

            UIRegistry.Register(_unitEntity, this);

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
        // Реактивный приём
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
                    State = buffer[i].State,
                    IsTracked = buffer[i].IsTracked
                });
            }

            Rebuild();
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

            Rebuild();
        }

        // ============================================================
        // Перестройка
        // ============================================================
        private void Rebuild()
        {
            if (_questContainer == null) return;

            var tracked = _quests.Find(q => q.IsTracked && q.State != QuestState.TurnedIn);
            bool shouldShow = !string.IsNullOrEmpty(tracked.QuestId);

            // Не дёргаем layout, если видимость не менялась
            if (shouldShow != _isShown)
            {
                _isShown = shouldShow;
                this.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!shouldShow) return;

            int hash = ComputeSnapshotHash(tracked.QuestId);
            if (hash == _lastSnapshotHash) return;
            _lastSnapshotHash = hash;

            _questContainer.Clear();

            var config = QuestsDatabase.GetQuest(tracked.QuestId);
            string title = config != null
                ? LocalizationManager.Get(config.title_key)
                : tracked.QuestId;

            // 🆕 Строка заголовка: название + кнопка «убрать из трекера»
            var headerRow = new VisualElement();
            headerRow.AddToClassList("quest-tracker__quest-header");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("quest-tracker__quest-title");
            headerRow.Add(titleLabel);

            var removeBtn = new Button();
            removeBtn.text = "×";
            removeBtn.AddToClassList("quest-tracker__quest-remove-btn");
            string capturedQuestId = tracked.QuestId;
            removeBtn.clicked += () => SendTrackQuest("");
            headerRow.Add(removeBtn);

            _questContainer.Add(headerRow);

            // Цели
            foreach (var obj in _objectives)
            {
                if (obj.QuestId != tracked.QuestId) continue;

                var objConfig = FindObjectiveConfig(config, obj.ObjectiveId);
                string objText = objConfig != null
                    ? LocalizationManager.Get(objConfig.text_key)
                    : obj.ObjectiveId;

                string prefix = obj.IsCompleted ? "✓" : "•";
                string progress = (!obj.IsCompleted && obj.Required > 1)
                    ? $" ({obj.Current}/{obj.Required})"
                    : "";

                var label = new Label($"{prefix} {objText}{progress}");
                label.AddToClassList("quest-tracker__objective");
                if (obj.IsCompleted) label.AddToClassList("quest-tracker__objective--completed");

                _questContainer.Add(label);
            }
        }

        private int ComputeSnapshotHash(string trackedId)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + trackedId.GetHashCode();

                for (int i = 0; i < _objectives.Count; i++)
                {
                    h = h * 31 + _objectives[i].QuestId.GetHashCode();
                    h = h * 31 + _objectives[i].ObjectiveId.GetHashCode();
                    h = h * 31 + _objectives[i].Current;
                    h = h * 31 + _objectives[i].Required;
                    h = h * 31 + (_objectives[i].IsCompleted ? 1 : 0);
                }

                h = h * 31 + (_collapsed ? 1 : 0);
                return h;
            }
        }


        private QuestObjective FindObjectiveConfig(QuestConfig config, string objectiveId)
        {
            if (config?.objectives == null) return null;
            foreach (var o in config.objectives)
                if (o.id == objectiveId) return o;
            return null;
        }

        // ============================================================
        // Кнопки
        // ============================================================
        private void OnCollapseClicked()
        {
            _collapsed = !_collapsed;
            if (_body != null)
                _body.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            if (_collapseBtn != null)
                _collapseBtn.text = _collapsed ? "+" : "–";
        }

        // 🆕 Снять трекинг (дублирует чекбокс в журнале)
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
    }
}

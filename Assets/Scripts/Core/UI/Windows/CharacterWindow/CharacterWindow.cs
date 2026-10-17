using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class CharacterWindow : UIWindow, ITabbedWindow
    {
        [Header("UI Components")]
        [SerializeField] private VisualTreeAsset _paperdollUxml;
        [SerializeField] private VisualTreeAsset _statsUxml;
        [SerializeField] private VisualTreeAsset _questJournalUxml;

        private HeaderComponent _header;
        private TabsComponent _tabsComponent;
        private CharacterSheet _characterSheet;
        private QuestJournal _questJournal;

        private VisualElement _pageCharacter;
        private VisualElement _pageQuestJournal;
        private VisualElement _pageSpellbook;

        private CharacterWindowTabs _currentTab = CharacterWindowTabs.Character;

        private Entity _playerEntity = Entity.Null;

        public override WindowType Type => WindowType.Character;
        public override Entity BoundEntity => _playerEntity;

        public override void Setup(Entity entity)
        {
            _playerEntity = entity != Entity.Null ? entity : PlayerUtils.GetEntityByTag<PlayerTag>();
            if (_root != null) _root.userData = _playerEntity;

            if (_playerEntity != Entity.Null)
            {
                _characterSheet?.BindToEntity(_playerEntity);
                _questJournal?.BindToEntity(_playerEntity);
            }

            Open();
        }

        public override void Close()
        {
            Unbind();
            base.Close();
        }

        public override void Unbind()
        {
            _characterSheet?.Unbind();
            _questJournal?.Unbind();
            _playerEntity = Entity.Null;
            if (_root != null) _root.userData = null;
        }

        protected override void OnWindowBuilt(VisualElement root)
        {
            // Хедер
            var headerSlot = root.Q<VisualElement>("header");
            if (headerSlot != null)
            {
                _header = new HeaderComponent { Title = LocalizationManager.Get("character_window") };
                _header.OnClose += Close;
                headerSlot.Add(_header);
                root.RegisterCallback<PointerDownEvent>(evt => root.BringToFront());
                var dragManipulator = new DragManipulator(_header, root, DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }

            // Табы
            var tabDefs = new List<TabDefinition>
            {
                new TabDefinition { Name = "tab-character-btn",     IconGlyph = "", Tooltip = "Персонаж" },
                new TabDefinition { Name = "tab-quest-journal-btn", IconGlyph = "", Tooltip = "Квесты" },
                new TabDefinition { Name = "tab-spellbook-btn",     IconGlyph = "", Tooltip = "Способности" },
            };


            _tabsComponent = new TabsComponent(
                root.Q<VisualElement>("tabs"),
                tabDefs,
                onChanged: idx => SwitchTab((CharacterWindowTabs)idx),
                activeClassName: "tabs__button--active",
                defaultIndex: (int)CharacterWindowTabs.Character
            );

            _pageCharacter = root.Q<VisualElement>("page-character");
            _pageQuestJournal = root.Q<VisualElement>("page-quest-journal");
            _pageSpellbook = root.Q<VisualElement>("page-spellbook");

            SwitchTab(CharacterWindowTabs.Character);

            // 🦾 Собираем дочерние компоненты ЗДЕСЬ, чтобы ScanAndCacheReceivers в UIWindow.InitializeWindow увидел их
            _characterSheet = new CharacterSheet(_pageCharacter, _statsUxml, _paperdollUxml);

            if (_questJournalUxml != null)
            {
                _questJournal = new QuestJournal(_questJournalUxml);
                _pageQuestJournal.Add(_questJournal);
            }
        }

        private void SwitchTab(CharacterWindowTabs tab)
        {
            _currentTab = tab;

            _pageCharacter.RemoveFromClassList("character-window__page--active");
            _pageQuestJournal.RemoveFromClassList("character-window__page--active");
            _pageSpellbook.RemoveFromClassList("character-window__page--active");

            switch (tab)
            {
                case CharacterWindowTabs.Character:
                    _pageCharacter.AddToClassList("character-window__page--active");
                    break;
                case CharacterWindowTabs.QuestJournal:
                    _pageQuestJournal.AddToClassList("character-window__page--active");
                    break;
                case CharacterWindowTabs.Spellbook:
                    _pageSpellbook.AddToClassList("character-window__page--active");
                    break;
            }
        }

        public void OpenOnTab(CharacterWindowTabs tab)
        {
            _tabsComponent?.SetActive((int)tab, notify: true);
        }

        public bool OpenOnTabId(string tabId)
        {
            if (string.IsNullOrEmpty(tabId))
                return true;   // нечего переключать — окно не просит закрыться

            CharacterWindowTabs? target = tabId switch
            {
                "character"     => CharacterWindowTabs.Character,
                "quest_journal" => CharacterWindowTabs.QuestJournal,
                "spellbook"     => CharacterWindowTabs.Spellbook,
                _ => null
            };

            if (target == null)
            {
                Debug.LogWarning($"[CharacterWindow] Неизвестный tabId: '{tabId}'");
                return true;   // не закрываем, просто игнорируем неизвестный токен
            }

            // Уже на этой вкладке → просим закрыться (toggle)
            if (_currentTab == target.Value)
            {
                Debug.Log($"[CharacterWindow] Уже на вкладке '{tabId}', просим закрыть.");
                return false;
            }

            // Переключаемся
            OpenOnTab(target.Value);
            return true;
        }
    }
}

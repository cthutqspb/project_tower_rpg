using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.ECS.Systems;

namespace ProjectTowerRpg.Core.UI.Components
{
    // 🦾 АБСОЛЮТНО САМОДОСТАТОЧНЫЙ СИ-КОМПОНЕНТ: Живёт на пустышке [MainMenu] внутри [UI]
    // Девственно слеп к UIWindow, WindowManager и HUD. У него своя, изолированная логика ОЗУ!
    public class MainMenu : MonoBehaviour
    {
        [Header("Ресурсы вёрстки меню")]
        [SerializeField] private VisualTreeAsset _menuUxml; // Наш чистый слот под MainMenu.uxml в инспекторе!

        private VisualElement _menuRoot;
        private PanelRenderer _panelRenderer;

        private Button _continueButton;
        private Button _newGameButton;
        private Button _saveButton;
        private Button _loadButton;
        private Button _exitButton;

        private void Start()
        {
            // Находим глобальный PanelRenderer сцены (как в твоих окнах!)
            _panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError("[MainMenu Component]: Корневой PanelRenderer не найден на сцене!");
                return;
            }

            // Подписываемся на нативный релоад панели через твой каноничный метод
            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);

            // Намертво привязываем переключение видимости к эвенту инпута по ESC
            UIEvents.OnToggleMainMenu += ToggleMenuVisibility;
        }

        private void OnDestroy()
        {
            // Наглухо выжигаем утечки памяти при уничтожении сцены
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }

            UIEvents.OnToggleMainMenu -= ToggleMenuVisibility;
            CleanUpCallbacks();
        }

        // 🦾 НАША СОБСТВЕННАЯ, ИЗОЛИРОВАННАЯ ЛОГИКА СБОРКИ В ОЗУ ЭКРАНА!
        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _menuUxml == null) return;

            // Защита от дублирования нод в ОЗУ экрана при горячей перезагрузке разметки
            if (_menuRoot != null && globalUiRoot.Contains(_menuRoot))
            {
                CleanUpCallbacks();
                globalUiRoot.Remove(_menuRoot);
            }

            // Клонируем вёрстку меню и нагло вживляем прямо в корень globalUiRoot — ПОВЕРХ ВСЕГО МИРА!
            _menuRoot = _menuUxml.CloneTree();
            //_menuRoot.pickingMode = PickingMode.Ignore;

            _menuRoot.pickingMode = PickingMode.Position;
            _menuRoot.style.position = Position.Absolute;
            _menuRoot.style.width = Length.Percent(100);
            _menuRoot.style.height = Length.Percent(100);

            globalUiRoot.Add(_menuRoot);

            // Изначально меню наглухо скрыто в ОЗУ экрана при старте игры
            _menuRoot.style.display = DisplayStyle.None;

            // Находим кнопки внутри нашей изолированной разметки
            _continueButton = _menuRoot.Q<Button>("btn-continue");
            _newGameButton = _menuRoot.Q<Button>("btn-new-game");
            _saveButton = _menuRoot.Q<Button>("btn-save");
            _loadButton = _menuRoot.Q<Button>("btn-load");
            _exitButton = _menuRoot.Q<Button>("btn-exit");

            // 🦾 СЛEПОЙ НАКАТ ЛOКАЛИЗАЦИИ: Переводим текст твоим нативным методом!
            if (_continueButton != null) _continueButton.text = LocalizationManager.Get("btn_continue_game");
            if (_newGameButton != null) _newGameButton.text = LocalizationManager.Get("btn_new_game");
            if (_saveButton != null) _saveButton.text = LocalizationManager.Get("btn_save_game");
            if (_loadButton != null) _loadButton.text = LocalizationManager.Get("btn_load_game");
            if (_exitButton != null) _exitButton.text = LocalizationManager.Get("btn_exit_game");

            // Вешаем Си-колбэки на клики мыши
            if (_continueButton != null) _continueButton.clicked += OnContinueClicked;
            if (_newGameButton != null) _newGameButton.clicked += OnNewGameClicked;
            if (_saveButton != null) _saveButton.clicked += OnSaveClicked;
            if (_loadButton != null) _loadButton.clicked += OnLoadClicked;
            if (_exitButton != null) _exitButton.clicked += OnExitClicked;
        }

        private void ToggleMenuVisibility()
        {
            if (_menuRoot == null) return;

            // Инвертируем стиль display прямо внутри globalUiRoot за 0 наносекунд нагрузки!
            bool isVisible = _menuRoot.style.display == DisplayStyle.Flex;
            _menuRoot.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
            
            Debug.Log($"[MainMenu Component]: Переключена видимость оверлея меню по ESC ➔ {_menuRoot.style.display}");
        }

        private void OnContinueClicked()
        {
            if (_menuRoot != null) _menuRoot.style.display = DisplayStyle.None;
            Debug.Log("[MainMenu Component]: Возврат в бесшовный мир.");
        }

        private void OnNewGameClicked()
        {
            Debug.Log("[MainMenu Component]: Сброс мира и запуск новой сессии...");
        }

        private void OnSaveClicked()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var saveSystem = world?.GetExistingSystemManaged<SaveSystem>();
            if (saveSystem != null) saveSystem.Save();
        }

        private void OnLoadClicked()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var saveSystem = world?.GetExistingSystemManaged<SaveSystem>();
            if (saveSystem != null) saveSystem.Load();
        }

        private void OnExitClicked()
        {
            Debug.Log("[MainMenu Component]: Выход на рабочий стол.");
            Application.Quit();
        }

        private void CleanUpCallbacks()
        {
            if (_continueButton != null) _continueButton.clicked -= OnContinueClicked;
            if (_newGameButton != null) _newGameButton.clicked -= OnNewGameClicked;
            if (_saveButton != null) _saveButton.clicked -= OnSaveClicked;
            if (_loadButton != null) _loadButton.clicked -= OnLoadClicked;
            if (_exitButton != null) _exitButton.clicked -= OnExitClicked;
        }
    }
}


using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("Вёрстка Скелета HUD")]
        [SerializeField] private VisualTreeAsset _hudUxml; 

        [Header("Настройки Юнит-Фреймов")]
        [SerializeField] private UnitFrame _playerFrame = new UnitFrame();
        [SerializeField] private UnitFrame _targetFrame = new UnitFrame();

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private bool _isUiReady = false;
        private bool _isPlayerBound = false; // 🔒 Замок-предохранитель

        private void Awake()
        {
            Instance = this;
            _panelRenderer = GetComponentInParent<PanelRenderer>();
        }

        private void Start()
        {
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void Update()
        {
            // 📡 ЛЕНИВЫЙ МОСТ СВЯЗИ: Ждем, пока фабрика ECS создаст игрока в ОЗУ
            if (_isUiReady && !_isPlayerBound)
            {
                Entity playerEntity = EntityRegistry.Get("player"); 
                if (playerEntity != Entity.Null)
                {
                    // 1. Привязываем фрейм игрока
                    _playerFrame.BindToEntity(playerEntity);
                    
                    // 2. Регистрируем фрейм ЦЕЛИ на сущность игрока
                    // Теперь чанк игрока DidChange-мигрирует, и UIPullSystem шёлково поймает Слайс Г!
                    UIRegistry.Register(playerEntity, _targetFrame);

                    _isPlayerBound = true; // Закрываем замок, Update больше не тратит тики процессора!
                    Debug.Log("⚡ [HUDManager]: Игрок найден! Оба фрейма (Персонаж и Цель) успешно встали на Pull-конвейер.");
                }
            }
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _hudUxml == null) return;

            // Защита от дублирования интерфейса при перезагрузке UI Toolkit
            if (_root != null && globalUiRoot.Contains(_root))
            {
                Entity oldPlayerEntity = EntityRegistry.Get("player");
                if (oldPlayerEntity != Entity.Null)
                {
                    UIRegistry.Unregister(oldPlayerEntity, _playerFrame);
                    UIRegistry.Unregister(oldPlayerEntity, _targetFrame);
                }

                globalUiRoot.Remove(_root);
                _isPlayerBound = false; // Сбрасываем флаг при полной перезагрузке панелей
            }

            // 1. Клонируем плоский скелет HUD
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore; 
            _root.style.flexGrow = 1;
            globalUiRoot.Add(_root);

            // 2. Находим слоты-пустышки в XML
            var playerSlot = _root.Q<VisualElement>("PlayerFrameSlot");
            var targetSlot = _root.Q<VisualElement>("TargetFrameSlot");

            if (playerSlot != null) _playerFrame.BuildFrame(playerSlot);
            if (targetSlot != null) _targetFrame.BuildFrame(targetSlot);

            // 3. Выставляем стартовую видимость и паспорта по умолчанию
            _playerFrame.UpdateIdentity("ПЕРСОНАЖ", 1);
            _playerFrame.SetVisible(true);

            _targetFrame.UpdateIdentity("ЦЕЛЬ", 1);
            _targetFrame.SetVisible(false); // Изначально скрыт
            

            // Если вдруг на момент перезагрузки панелей игрок уже был в реестре — биндимся сразу
            Entity playerEntity = EntityRegistry.Get("player"); 
            if (playerEntity != Entity.Null)
            {
                _playerFrame.BindToEntity(playerEntity);
                UIRegistry.Register(playerEntity, _targetFrame);
                _isPlayerBound = true;
            }

            _isUiReady = true;
            Debug.Log("[HUDManager]: Все фреймы шёлково собраны внутри своих слотов!");
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }

            Entity playerEntity = EntityRegistry.Get("player");
            if (playerEntity != Entity.Null)
            {
                UIRegistry.Unregister(playerEntity, _playerFrame);
                UIRegistry.Unregister(playerEntity, _targetFrame);
            }
        }
    }
}


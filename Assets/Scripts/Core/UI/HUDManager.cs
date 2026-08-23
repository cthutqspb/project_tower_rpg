using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.ECS.Components;
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
            if (_isUiReady && !_isPlayerBound)
            {
                Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (playerEntity != Entity.Null)
                {
                    _playerFrame.BindToEntity(playerEntity);
                    UIRegistry.Register(playerEntity, _targetFrame);
                    _isPlayerBound = true;
                    Debug.Log("⚡ [HUDManager]: Игрок найден! Фреймы встали на Pull-конвейер.");
                }
            }
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _hudUxml == null) return;

            // Защита от дублирования интерфейса при перезагрузке UI Toolkit
            if (_root != null && globalUiRoot.Contains(_root))
            {
                Entity oldPlayerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (oldPlayerEntity != Entity.Null)
                {
                    UIRegistry.Unregister(oldPlayerEntity, _playerFrame);
                    UIRegistry.Unregister(oldPlayerEntity, _targetFrame);
                }

                globalUiRoot.Remove(_root);
                _isPlayerBound = false;
            }

            // 1. Клонируем плоский скелет HUD
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore;
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
            _targetFrame.SetVisible(false);

            // Если вдруг на момент перезагрузки панелей игрок уже был в реестре — биндимся сразу
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
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

            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                UIRegistry.Unregister(playerEntity, _playerFrame);
                UIRegistry.Unregister(playerEntity, _targetFrame);
            }
        }     
    }
}


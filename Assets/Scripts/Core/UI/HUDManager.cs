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
        [SerializeField] private VisualTreeAsset _hudUxml; // Сюда HUD.uxml

        [Header("Настройки Юнит-Фреймов")]
        [SerializeField] private UnitFrame _playerFrame = new UnitFrame();
        [SerializeField] private UnitFrame _targetFrame = new UnitFrame();

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private bool _isUiReady = false;

        private void Awake()
        {
            Instance = this;
            _panelRenderer = GetComponentInParent<PanelRenderer>();
        }

        private void Start()
        {
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _hudUxml == null) return;

            // Защита от дублирования интерфейса при перезагрузке UI Toolkit
            if (_root != null && globalUiRoot.Contains(_root))
            {
                // Выписываем старую связь из конвейера перед удалением вёрстки
                Entity oldPlayerEntity = EntityRegistry.Get("player");
                if (oldPlayerEntity != Entity.Null)
                {
                    UIRegistry.Unregister(oldPlayerEntity, _playerFrame);
                }

                globalUiRoot.Remove(_root);
            }

            // 1. Клонируем плоский скелет HUD
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore; // Твой бронебойный фикс мыши!
            _root.style.flexGrow = 1;
            globalUiRoot.Add(_root);

            // 2. Находим слоты-пустышки в XML
            var playerSlot = _root.Q<VisualElement>("PlayerFrameSlot");
            var targetSlot = _root.Q<VisualElement>("TargetFrameSlot");

            // Запускаем внутреннюю сборку компонентов (UXML фрейма + полосок) внутри найденных слотов
            if (playerSlot != null) _playerFrame.BuildFrame(playerSlot);
            if (targetSlot != null) _targetFrame.BuildFrame(targetSlot);

            // 3. Выставляем стартовую видимость и паспорта по умолчанию
            _playerFrame.UpdateIdentity("ПЕРСОНАЖ", 1);
            _playerFrame.SetVisible(true);

            _targetFrame.UpdateIdentity("ЦЕЛЬ", 1);
            _targetFrame.SetVisible(false); // Таргет скрыт, ждёт выделения сущности

            // =========================================================================
            // 🌟 ТВОЙ ЧИСТЫЙ КАНОН: Связываем фрейм игрока с ECS в одну строчку!
            // =========================================================================
            Entity playerEntity = EntityRegistry.Get("player"); 
            if (playerEntity != Entity.Null)
            {
                _playerFrame.BindToEntity(playerEntity);
            }
            else
            {
                Debug.LogWarning("[HUDManager]: Сущность 'player' ещё не зарегистрирована в EntityRegistry. Ожидаю спавна.");
            }


            _isUiReady = true;
            Debug.Log("[HUDManager]: Все фреймы шёлково собраны внутри своих слотов и переведены на Pull-конвейер!");
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }

            // Чистим подписки при уничтожении объекта
            Entity playerEntity = EntityRegistry.Get("player");
            if (playerEntity != Entity.Null)
            {
                UIRegistry.Unregister(playerEntity, _playerFrame);
            }
        }
    }
}


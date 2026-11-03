using UnityEngine;
using System.Collections.Generic;
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

        // =========================================================================
        // 🔮 НАСТРОЙКИ ЭКШЕН-БАРОВ (Каноничный список под динамическое количество)
        // =========================================================================
        [Header("Настройки Экшен-баров")]
        [SerializeField] private VisualTreeAsset _actionBarUxml; // Наш единый UXML-шаблон панели на 12 слотов
        [SerializeField] private List<ActionBar> _actionBars = new List<ActionBar>(); // Список самих панелей

        [Header("Настройки Кастбара")]
        [SerializeField] private VisualTreeAsset _progressBarUxml; // Наш универсальный шаблон полоски!

        private CastBar _castBar;

        [Header("Настройки Чат-Лога")]
        [SerializeField] private VisualTreeAsset _chatLogUxml; 
        private ChatLog _chatLog;

        [Header("Настройки Диалогов NPC")]
        [SerializeField] private VisualTreeAsset _dialogueOverlayUxml;
        private DialogueOverlay _dialogueOverlay;

        [Header("Настройки Трекера Квестов")]
        [SerializeField] private VisualTreeAsset _questTrackerUxml;
        private QuestTracker _questTracker;

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private bool _isUiReady = false;
        private bool _isPlayerBound = false; 
        
        // ✅ Храним стейт последней цели, чтобы не спамить реестр перерегистрациями каждый кадр
        private Entity _lastTargetEntity = Entity.Null; 
        private Entity _lastControlledEntity = Entity.Null;

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
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 1. Биндим фрейм игрока (только один раз)
            if (_isUiReady && !_isPlayerBound)
            {
                _playerFrame.BindToEntity(playerEntity);
                PushInitialState(playerEntity, _playerFrame);
                _playerFrame.SetVisible(true);
                _isPlayerBound = true;
            }

            // 2. Биндим экшен-бары
            if (_isUiReady && _actionBars != null)
            {
                for (int i = 0; i < _actionBars.Count; i++)
                {
                    var grid = _actionBars[i].SlotsGrid;
                    if (grid != null && grid.BoundEntity != playerEntity)
                    {
                        grid.BindToEntity(playerEntity);
                    }
                }
            }

            // 3. Биндим диалоговый оверлей
            if (_isUiReady && _dialogueOverlay != null && _dialogueOverlay.BoundEntity != playerEntity)
            {
                _dialogueOverlay.BindToEntity(playerEntity);
                UIRegistry.Register(playerEntity, _dialogueOverlay);
            }

            // 4. Биндим трекер квестов
            if (_isUiReady && _questTracker != null && _questTracker.BoundEntity != playerEntity)
            {
                _questTracker.BindToEntity(playerEntity);
                UIRegistry.Register(playerEntity, _questTracker);
            }

            // 5. Биндим кастбар
            if (_isUiReady)
            {
                Entity currentControlled = PlayerUtils.GetEntityByTag<ControlledByPlayerTag>();
                if (currentControlled != _lastControlledEntity)
                {
                    if (_lastControlledEntity != Entity.Null && _castBar != null)
                        UIRegistry.Unregister(_lastControlledEntity, _castBar);

                    if (currentControlled != Entity.Null && _castBar != null)
                    {
                        UIRegistry.Register(currentControlled, _castBar);
                        if (em.HasComponent<CastComponent>(currentControlled))
                        {
                            var castComp = em.GetComponentData<CastComponent>(currentControlled);
                            _castBar.UpdateFromComponent(ref castComp);
                        }
                    }
                    _lastControlledEntity = currentControlled;
                }
            }

            // 🎯 6. БИНДИМ ТАРГЕТ-ФРЕЙМ (с защитой от самого себя)
            if (_isUiReady && _isPlayerBound && em.HasComponent<CombatStateComponent>(playerEntity))
            {
                Entity currentTarget = em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

                // Защита: если таргет — это сам игрок, считаем, что таргета нет
                if (currentTarget == playerEntity)
                    currentTarget = Entity.Null;

                if (currentTarget != _lastTargetEntity)
                {
                    _targetFrame.BindToEntity(currentTarget);
                    if (currentTarget == Entity.Null)
                    {
                        _targetFrame.SetVisible(false);
                    }
                    else
                    {
                        PushInitialState(currentTarget, _targetFrame);
                        _targetFrame.SetVisible(true);
                    }
                    _lastTargetEntity = currentTarget;
                }
            }
        }

        private void PushInitialState(Entity target, object receiver)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (receiver is IEcsUiComponentReceiver<UnitComponent> unitUi && em.HasComponent<UnitComponent>(target))
            {
                var comp = em.GetComponentData<UnitComponent>(target);
                unitUi.UpdateFromComponent(ref comp);
            }
            if (receiver is IEcsUiComponentReceiver<HealthComponent> healthUi && em.HasComponent<HealthComponent>(target))
            {
                var comp = em.GetComponentData<HealthComponent>(target);
                healthUi.UpdateFromComponent(ref comp);
            }
            if (receiver is IEcsUiComponentReceiver<ResourceComponent> resUi && em.HasComponent<ResourceComponent>(target))
            {
                var comp = em.GetComponentData<ResourceComponent>(target);
                resUi.UpdateFromComponent(ref comp);
            }
        }


        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _hudUxml == null) return;

            // =========================================================================
            // 1. ОЧИСТКА СТАРОГО ДЕРЕВА (при перезагрузке UI)
            // =========================================================================
            if (_root != null && globalUiRoot.Contains(_root))
            {
                Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();

                // Отписываем всё, что было зарегистрировано
                if (playerEntity != Entity.Null)
                {
                    UIRegistry.Unregister(playerEntity, _playerFrame);
                    UIRegistry.Unregister(playerEntity, _dialogueOverlay);
                    UIRegistry.Unregister(playerEntity, _questTracker);
                }

                if (_lastTargetEntity != Entity.Null)
                    UIRegistry.Unregister(_lastTargetEntity, _targetFrame);

                if (_lastControlledEntity != Entity.Null && _castBar != null)
                    UIRegistry.Unregister(_lastControlledEntity, _castBar);

                if (_chatLog != null)
                {
                    _chatLog.Dispose();
                    _chatLog = null;
                }

                if (_actionBars != null)
                {
                    for (int i = 0; i < _actionBars.Count; i++)
                    {
                        var actionBarGrid = _actionBars[i].SlotsGrid;
                        if (actionBarGrid != null && actionBarGrid.BoundEntity != Entity.Null)
                            UIRegistry.Unregister(actionBarGrid.BoundEntity, actionBarGrid);
                    }
                }

                globalUiRoot.Remove(_root);
                _root = null;

                // Сбрасываем стейт
                _isPlayerBound = false;
                _lastTargetEntity = Entity.Null;
                _lastControlledEntity = Entity.Null;
            }

            // =========================================================================
            // 2. СБОРКА HUD (всё создаётся заново, но без биндинга)
            // =========================================================================
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            globalUiRoot.Add(_root);

            // Фреймы (создаём визуал)
            var playerSlot = _root.Q<VisualElement>("player-frame-slot");
            var targetSlot = _root.Q<VisualElement>("target-frame-slot");
            if (playerSlot != null) _playerFrame.BuildFrame(playerSlot);
            if (targetSlot != null) _targetFrame.BuildFrame(targetSlot);

            // Экшен-бары
            if (_actionBarUxml != null && _actionBars != null)
            {
                for (int i = 0; i < _actionBars.Count; i++)
                    _actionBars[i].BuildPanel(_root, _actionBarUxml, i);
            }

            // Кастбар
            if (_progressBarUxml != null)
            {
                var castBarVisual = _progressBarUxml.CloneTree();
                castBarVisual.name = "CastBar";
                castBarVisual.style.position = Position.Absolute;
                castBarVisual.style.left = Length.Percent(42f);
                castBarVisual.style.top = Length.Percent(70f);
                _castBar = new CastBar(castBarVisual);
                _root.Add(castBarVisual);
            }

            // Чат-лог
            if (_chatLogUxml != null)
            {
                var chatRoot = _chatLogUxml.CloneTree();
                chatRoot.name = "chat-log";
                chatRoot.AddToClassList("hud__chat-log");
                _chatLog = new ChatLog(chatRoot);
                _root.Add(chatRoot);
            }

            // Диалоговый оверлей
            if (_dialogueOverlayUxml != null)
            {
                var dialogueRoot = _dialogueOverlayUxml.CloneTree();
                dialogueRoot.name = "dialogue-overlay";
                dialogueRoot.AddToClassList("hud__dialogue-overlay");
                dialogueRoot.pickingMode = PickingMode.Ignore;

                _dialogueOverlay = gameObject.GetComponent<DialogueOverlay>();
                if (_dialogueOverlay == null)
                    _dialogueOverlay = gameObject.AddComponent<DialogueOverlay>();

                _dialogueOverlay.Initialize(dialogueRoot);
                _root.Add(dialogueRoot);
            }

            // Трекер квестов
            if (_questTrackerUxml != null)
            {
                _questTracker = new QuestTracker(_questTrackerUxml);
                _root.Add(_questTracker);
            }

            _isUiReady = true;
            Debug.Log("[HUDManager]: HUD собран заново после релоада.");
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);

            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                UIRegistry.Unregister(playerEntity, _playerFrame);
            }

            if (_lastTargetEntity != Entity.Null)
            {
                UIRegistry.Unregister(_lastTargetEntity, _targetFrame);
            }

            if (_lastControlledEntity != Entity.Null && _castBar != null)
            {
                UIRegistry.Unregister(_lastControlledEntity, _castBar);
            }

            if (playerEntity != Entity.Null && _dialogueOverlay != null)
            {
                UIRegistry.Unregister(playerEntity, _dialogueOverlay);
            }

            if (playerEntity != Entity.Null && _questTracker != null)
            {
                UIRegistry.Unregister(playerEntity, _questTracker);
            }
        }     
    }
}

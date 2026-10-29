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
            // 1. Вытаскиваем биологическую сущность игрока (для фиксированного фрейма)
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Старая легальная привязка фрейма игрока (остается неизменной)
            if (_isUiReady && !_isPlayerBound)
            {
                _playerFrame.BindToEntity(playerEntity);
                _isPlayerBound = true;
            }

            // =========================================================================
            // 🦾 ИДЕМПОТЕНТНЫЙ БИНД ЭКШЕН-БАРОВ (Без флагов, работает при респавне/релоаде)
            // =========================================================================
            if (_isUiReady && _actionBars != null)
            {
                for (int i = 0; i < _actionBars.Count; i++)
                {
                    var grid = _actionBars[i].SlotsGrid;
                    // Биндим только если сетка создана и ещё не привязана к этому игроку
                    if (grid != null && grid.BoundEntity != playerEntity)
                    {
                        grid.BindToEntity(playerEntity);
                        Debug.Log($"[HUDManager]: Экшен-бар {i} забинжен на игрока {playerEntity.Index}");
                    }
                }
            }

            // =========================================================================
            // 🦾 ИДЕМПОТЕНТНЫЙ БИНД ДИАЛОГОВОГО ОВЕРЛЕЯ
            // =========================================================================
            if (_isUiReady && _dialogueOverlay != null)
            {
                // Проверяем, привязан ли оверлей к игроку
                if (_dialogueOverlay.BoundEntity != playerEntity)
                {
                    _dialogueOverlay.BindToEntity(playerEntity);
                    UIRegistry.Register(playerEntity, _dialogueOverlay);
                    Debug.Log($"[HUDManager]: Диалоговый оверлей забинжен на игрока {playerEntity.Index}");
                }
            }

            // =========================================================================
            // 🦾 ИДЕМПОТЕНТНЫЙ БИНД ТРЕКЕРА КВЕСТОВ
            // =========================================================================
            if (_isUiReady && _questTracker != null)
            {
                // Проверяем, привязан ли трекер к игроку (нужно свойство BoundEntity у QuestTracker)
                if (_questTracker.BoundEntity != playerEntity)
                {
                    _questTracker.BindToEntity(playerEntity);
                    UIRegistry.Register(playerEntity, _questTracker);
                    Debug.Log($"[HUDManager]: Трекер квестов забинжен на игрока {playerEntity.Index}");
                }
            }

            // =========================================================================
            // 🦾 ДИНАМИЧЕСКИЙ СИ-МОСТ КАСTБАРA (Wow-Канон Майнд-контроля):
            // =========================================================================
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

                    Debug.Log($"[HUDManager]: Кастбар перехвачен! Новый актор инпута: {currentControlled}");
                    _lastControlledEntity = currentControlled;
                }
            }

            // =========================================================================
            // Твой старый отлаженный код трекинга Таргет-Фрейма (_targetFrame)...
            // =========================================================================
            if (_isPlayerBound && em.HasComponent<CombatStateComponent>(playerEntity))
            {
                Entity currentTarget = em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

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

            // Защита от дублирования интерфейса при перезагрузке UI Toolkit
            if (_root != null && globalUiRoot.Contains(_root))
            {
                Entity oldPlayerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (oldPlayerEntity != Entity.Null)
                {
                    UIRegistry.Unregister(oldPlayerEntity, _playerFrame);
                }

                if (_lastControlledEntity != Entity.Null && _castBar != null) 
                {    
                    UIRegistry.Unregister(_lastControlledEntity, _castBar);

                    _lastControlledEntity = Entity.Null;
                    _castBar = null;
                }

                if (_lastTargetEntity != Entity.Null)
                {
                    UIRegistry.Unregister(_lastTargetEntity, _targetFrame);
                }

                if (_chatLog != null) 
                {
                    _chatLog.Dispose();
                    _chatLog = null;
                }

                if (_dialogueOverlay != null)
                {
                    if (oldPlayerEntity != Entity.Null)
                        UIRegistry.Unregister(oldPlayerEntity, _dialogueOverlay);

                    _dialogueOverlay.BindToEntity(Entity.Null);
                    _dialogueOverlay = null;
                }

                if (_questTracker != null)
                {
                    Entity oldPlayer = PlayerUtils.GetEntityByTag<PlayerTag>();
                    if (oldPlayer != Entity.Null)
                        UIRegistry.Unregister(oldPlayer, _questTracker);
                    _questTracker = null;
                }

                if (_actionBars != null)
                {
                    for (int i = 0; i < _actionBars.Count; i++)
                    {
                        var oldGrid = _actionBars[i].SlotsGrid;
                        if (oldGrid != null && oldGrid.BoundEntity != Entity.Null)
                            UIRegistry.Unregister(oldGrid.BoundEntity, oldGrid);
                    }
                }

                globalUiRoot.Remove(_root);
                _isPlayerBound = false;
                _lastTargetEntity = Entity.Null; 
            }

            // 1. Клонируем плоский скелет HUD
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            globalUiRoot.Add(_root);

            // 2. Находим слоты-пустышки в XML и собираем в них фреймы
            var playerSlot = _root.Q<VisualElement>("player-frame-slot");
            var targetSlot = _root.Q<VisualElement>("target-frame-slot");

            if (playerSlot != null) _playerFrame.BuildFrame(playerSlot);
            if (targetSlot != null) _targetFrame.BuildFrame(targetSlot);

            // 3. ✅ ИСПРАВЛЕННАЯ СИНХРОНИЗАЦИЯ ПРИ ПЕРЕЗАГРУЗКЕ HUD
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                _playerFrame.BindToEntity(playerEntity);
                PushInitialState(playerEntity, _playerFrame);
                _playerFrame.SetVisible(true);                
                
                _isPlayerBound = true;

                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (em.HasComponent<CombatStateComponent>(playerEntity))
                {
                    Entity currentTarget = em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

                    if (currentTarget != Entity.Null)
                    {
                        _targetFrame.BindToEntity(currentTarget);
                        PushInitialState(currentTarget, _targetFrame);
                        _targetFrame.SetVisible(true);                        
                        _lastTargetEntity = currentTarget;
                    }
                    else
                    {
                        _targetFrame.BindToEntity(Entity.Null);
                        _targetFrame.SetVisible(false);
                        _lastTargetEntity = Entity.Null;
                    }
                }
            }

            // =========================================================================
            // 🦾 СБОРКА ЭКШЕН-БАРОВ (БЕЗ БИНДИНГА — БИНДИНГ ТЕПЕРЬ В UPDATE)
            // =========================================================================
            if (_actionBarUxml != null && _actionBars != null)
            {
                for (int i = 0; i < _actionBars.Count; i++)
                {
                    _actionBars[i].BuildPanel(_root, _actionBarUxml, i);
                }
                Debug.Log("[HUDManager]: Экшен-бары собраны. Ожидаем спавна игрока для биндинга в Update().");
            }

            // 🦾 КАНOНИЧНАЯ СБOРКА ДИАЛОГОВОГО ОВЕРЛЕЯ (ТОЛЬКО СОЗДАНИЕ, БЕЗ БИНДИНГА)
            if (_dialogueOverlayUxml != null && _root != null)
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

            // 🦾 КАНOНИЧНАЯ СБOРКА КАСTБАРA ИГРОКА По центру экрана:
            if (_progressBarUxml != null && _root != null)
            {
                var castBarVisual = _progressBarUxml.CloneTree();
                castBarVisual.name = "CastBar"; 
                castBarVisual.style.position = Position.Absolute;
                castBarVisual.style.left = Length.Percent(42f);
                castBarVisual.style.top = Length.Percent(70f);

                _castBar = new CastBar(castBarVisual);
                _root.Add(castBarVisual);
            }

            // 🦾 КАНOНИЧНАЯ СБОРКА ЧАТ-ЛОГА:
            if (_chatLogUxml != null && _root != null)
            {
                var chatRoot = _chatLogUxml.CloneTree();
                chatRoot.name = "chat-log";
                chatRoot.AddToClassList("hud__chat-log");
                _chatLog = new ChatLog(chatRoot);
                _root.Add(chatRoot);
            }

            // 🦾 СБОРКА ТРЕКЕРА КВЕСТОВ:
            if (_questTrackerUxml != null && _root != null)
            {
                _questTracker = new QuestTracker(_questTrackerUxml);
                _root.Add(_questTracker);            
            }

            _isUiReady = true;
            Debug.Log("[HUDManager]: Все фреймы шёлково собраны внутри своих слотов после релоада!");
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

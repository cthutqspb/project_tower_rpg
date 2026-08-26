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

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private bool _isUiReady = false;
        private bool _isPlayerBound = false; 
        
        // ✅ Храним стейт последней цели, чтобы не спамить реестр перерегистрациями каждый кадр
        private Entity _lastTargetEntity = Entity.Null; 

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

            if (_isUiReady && !_isPlayerBound)
            {
                _playerFrame.BindToEntity(playerEntity);
                _isPlayerBound = true;
            }

            if (_isPlayerBound && em.HasComponent<CombatStateComponent>(playerEntity))
            {
                Entity currentTarget = em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

                if (currentTarget != _lastTargetEntity)
                {
                    // Атомарно перевешиваем рельсы в реестре
                    _targetFrame.BindToEntity(currentTarget);

                    if (currentTarget == Entity.Null)
                    {
                        _targetFrame.SetVisible(false);
                    }
                    else
                    {
                        // ✅ ВМЕСТО ПОРТЯНКИ IF:
                        // Мы просто просим UIPullSystem (или пишем хелпер в UIRegistry), 
                        // чтобы она прямо сейчас принудительно вызвала методы конвейера для этой новой Entity.
                        // Нам не нужно руками читать компоненты! Мы говорим: "Эй, прогони по конвейеру компоненты скелета для _targetFrame"

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

                // Выписываем фрейм цели из реестра, если он был к кому-то привязан
                if (_lastTargetEntity != Entity.Null)
                {
                    UIRegistry.Unregister(_lastTargetEntity, _targetFrame);
                }

                globalUiRoot.Remove(_root);
                _isPlayerBound = false;
                _lastTargetEntity = Entity.Null; 
            }

            // 1. Клонируем плоский скелет HUD
            _root = _hudUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore;
            // =========================================================================
            // 🦾 ЖЕЛЕЗОБЕТОННЫЙ РАСТЯГ ОБЕРТКИ (Фронтенд-канон UI Toolkit):
            // Нагло заставляем внешнюю невидимую коробку занять 100% экрана,
            // чтобы внутренний .hud и твои проценты панелей считались от реального разрешения!
            // =========================================================================
            _root.style.position = Position.Absolute;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            globalUiRoot.Add(_root);

            // 2. Находим слоты-пустышки в XML и собираем в них фреймы
            var playerSlot = _root.Q<VisualElement>("PlayerFrameSlot");
            var targetSlot = _root.Q<VisualElement>("TargetFrameSlot");

            if (playerSlot != null) _playerFrame.BuildFrame(playerSlot);
            if (targetSlot != null) _targetFrame.BuildFrame(targetSlot);

            // 3. ✅ ИСПРАВЛЕННАЯ СИНХРОНИЗАЦИЯ ПРИ ПЕРЕЗАГРУЗКЕ HUD
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                // Биндим игрока и сразу заливаем в его фрейм актуальный стейт из ECS
                _playerFrame.BindToEntity(playerEntity);
                PushInitialState(playerEntity, _playerFrame);
                _playerFrame.SetVisible(true);                
                
                _isPlayerBound = true;

                // Проверяем, была ли у игрока активная цель до релоада панелей
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (em.HasComponent<CombatStateComponent>(playerEntity))
                {
                    Entity currentTarget = em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

                    if (currentTarget != Entity.Null)
                    {
                        // Если цель была — мгновенно восстанавливаем её линк в UIRegistry и рендерим
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
            // 🦾 СБОРКА И БИНД ЭКШЕН-БАРОВ НА СУЩНОСТЬ ИГРОКА
            // =========================================================================
                        if (_actionBarUxml != null && _actionBars != null)
            {
                // 1. Создаем геометрию панелей
                for (int i = 0; i < _actionBars.Count; i++)
                {
                    _actionBars[i].BuildPanel(_root, _actionBarUxml, i);
                }

                // 2. 🦾 НАПРЯМУЮ БИНДИМ НА СУЩНОСТЬ САМОГО ИГРОКА:
                if (playerEntity != Entity.Null)
                {
                    for (int i = 0; i < _actionBars.Count; i++)
                    {
                        // Сетки панелей скачают буфер ActionBarSlot прямо из башки игрока!
                        _actionBars[i].SlotsGrid.BindToEntity(playerEntity);
                    }
                    Debug.Log("[HUDManager]: Все экшен-бары шёлково привязаны к буферу ActionBarSlot Игрока!");
                }
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
        }     
    }
}


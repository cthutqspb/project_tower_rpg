using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.Transforms;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.Core.UI
{
    public class NameplateManager : MonoBehaviour
    {
        public static NameplateManager Instance { get; private set; }

        [Header("Ресурсы вёрстки неймплейтов")]
        [SerializeField] private VisualTreeAsset _nameplatesUxml;
        
        [Header("Настройки")]
        [SerializeField] private GameSettings _settings;

        [Header("Пул")]
        [SerializeField] private int _poolSize = 64;

        private VisualElement _root;
        private VisualElement _container;
        private PanelRenderer _panelRenderer;
        private VisualElement _globalUiRoot;

        private readonly Dictionary<Entity, NameplateElement> _active = new();
        private readonly Queue<NameplateElement> _pool = new();

        private EntityManager _em;
        private EntityQuery _query;

        private void Awake() { Instance = this; }

        private void Start()
        {
            _panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer == null) { Debug.LogError("[NameplateManager]: PanelRenderer не найден!"); return; }

            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                _em = world.EntityManager;
                _query = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<NameplateStateComponent>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<CombatStateComponent>() 
                );
            }
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            if (Instance == this) Instance = null;
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _nameplatesUxml == null) return;

            _globalUiRoot = globalUiRoot;

            if (_root != null && globalUiRoot.Contains(_root)) globalUiRoot.Remove(_root);

            _root = _nameplatesUxml.CloneTree();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            _root.style.zIndex = -1;
            globalUiRoot.Insert(0, _root);

            _container = _root.Q<VisualElement>("root");

            foreach (var widget in _active.Values) widget.SetActive(false);
            _active.Clear();
            _pool.Clear();

            for (int i = 0; i < _poolSize; i++)
            {
                var widget = new NameplateElement();
                widget.SetActive(false);
                _container.Add(widget);
                _pool.Enqueue(widget);
            }
        }

        private void LateUpdate()
        {
            if (_container == null || _globalUiRoot == null || _settings == null) return;

            var camera = Camera.main;
            if (camera == null) return;

            var uiPanel = _globalUiRoot.panel;
            if (uiPanel == null) return;

            // 🆕 Игрок и его текущая цель — один раз за кадр
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            Entity playerTarget = Entity.Null;
            if (playerEntity != Entity.Null && _em.HasComponent<CombatStateComponent>(playerEntity))
                playerTarget = _em.GetComponentData<CombatStateComponent>(playerEntity).CurrentTarget;

            // 1. Скрываем тех, кто умер, пропал, за камерой или выключен настройками
            var entitiesToRelease = new List<Entity>();
            foreach (var pair in _active)
            {
                if (!_em.Exists(pair.Key)) { entitiesToRelease.Add(pair.Key); continue; }
                
                var state = _em.GetComponentData<NameplateStateComponent>(pair.Key);
                if (!ShouldShowNameplate(pair.Key, state, camera, out _)) 
                    entitiesToRelease.Add(pair.Key);
            }
            foreach (var e in entitiesToRelease) ReleaseWidget(e);

            // 2. Обновляем активных и добавляем новых
            var entities = _query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var state = _em.GetComponentData<NameplateStateComponent>(entity);
                
                // Проверка + получение worldPos одним вызовом
                if (!ShouldShowNameplate(entity, state, camera, out Vector3 worldPos)) continue;

                var widget = GetWidget(entity);
                if (widget == null) continue;

                widget.BoundEntity = entity;

                Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(uiPanel, worldPos, camera);
                widget.SetPosition(panelPos.x, panelPos.y);

                float distance = Vector3.Distance(camera.transform.position, worldPos);
                widget.SetDepth(Mathf.RoundToInt(1000f - distance));

                // Данные
                string name = LocalizationManager.Get(state.NameKey.ToString());
                widget.SetName(name);
                widget.SetMark(state.Mark);

                var combat = _em.GetComponentData<CombatStateComponent>(entity);
                widget.SetRelation(combat.Relation);

                // 🆕 Подсветка текущей цели
                widget.SetTargeted(entity == playerTarget);
            }
            entities.Dispose();
        }

        // 🆕 Проверка + вычисление мировой позиции (с проверкой на экран)
        private bool ShouldShowNameplate(Entity entity, NameplateStateComponent state, Camera camera, out Vector3 worldPos)
        {
            worldPos = default;

            // 1. Настройки
            bool isPlayer = _em.HasComponent<PlayerTag>(entity);
            var combat = _em.GetComponentData<CombatStateComponent>(entity);

            if (isPlayer && !_settings.ShowPlayerNameplate) return false;
            
            if (!isPlayer)
            {
                if (combat.Relation == NameplateRelation.Hostile && !_settings.ShowEnemyNameplates) return false;
                if (combat.Relation != NameplateRelation.Hostile && !_settings.ShowNpcNameplates) return false;
            }

            // 2. Дистанция (куллинг)
            var transform = _em.GetComponentData<LocalTransform>(entity);
            Vector3 entityPos = transform.Position;
            float distance = Vector3.Distance(camera.transform.position, entityPos);
            if (distance > _settings.MaxViewDistance) return false;

            // 3. Экранная позиция
            worldPos = entityPos + new Vector3(0f, 2.2f, 0f);
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPos);

            // За камерой?
            if (screenPoint.z <= 0) return false;

            // За пределами экрана? (строго, без margin)
            if (screenPoint.x < 0 || screenPoint.x > Screen.width) return false;
            if (screenPoint.y < 0 || screenPoint.y > Screen.height) return false;

            return true;
        }

        private NameplateElement GetWidget(Entity entity)
        {
            if (_active.TryGetValue(entity, out var widget)) return widget;
            if (_pool.Count == 0) return null;

            widget = _pool.Dequeue();
            widget.SetActive(true);
            _active[entity] = widget;
            return widget;
        }

        private void ReleaseWidget(Entity entity)
        {
            if (_active.TryGetValue(entity, out var widget))
            {
                widget.SetActive(false);
                _active.Remove(entity);
                _pool.Enqueue(widget);
            }
        }
    }
}

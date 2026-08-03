using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.Core.UI;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class CharacterWindow : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset _windowUxml;
        
        private VisualElement _root;
        private HeaderComponent _header;
        private StaticGrid _inventoryGrid;
        private PanelRenderer _panelRenderer;

        private void Start()
        {
            _panelRenderer = FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError("[CharacterWindow]: PanelRenderer не найден!");
                return;
            }

            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null || _root != null) return;

            _root = _windowUxml.CloneTree();
            _root.pickingMode = PickingMode.Position;
            globalUiRoot.Add(_root);

            var headerContainer = _root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                _header = new HeaderComponent();
                _header.Title = "РЮКЗАК ПЕРСОНАЖА";
                _header.OnClose += CloseWindow;
                headerContainer.Add(_header);

                var dragManipulator = new DragManipulator(
                    dragElement: _header,
                    targetElement: _root,
                    mode: DragMode.UIElement
                );
                _header.AddManipulator(dragManipulator);
            }

            var inventoryContainer = _root.Q<VisualElement>("inventory-container");
            if (inventoryContainer != null)
            {
                _inventoryGrid = new StaticGrid(columns: 6, rows: 4, gridType: "inventory");
                _inventoryGrid.DataSourceId = "unit_inventory";
                inventoryContainer.Add(_inventoryGrid);

                LinkAndInitializeInventory();
            }

            var paperdollContainer = _root.Q<VisualElement>("paperdoll-container");
            if (paperdollContainer != null)
            {
                // TODO: PaperdollComponent
            }

            ShowWindow();
            Debug.Log("🎯 [CharacterWindow]: Окно собрано из компонентов!");
        }

        private void LinkAndInitializeInventory()
        {
            if (World.DefaultGameObjectInjectionWorld == null) return;

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var inventoryEntity = EntityRegistry.Get("unit_inventory");

            if (inventoryEntity == Entity.Null)
            {
                Debug.LogWarning("[CharacterWindow] Сущность инвентаря ещё не создана в ECS/реестре");
                return;
            }

            // ✅ НОВАЯ АРХИТЕКТУРА: BindToEntity регистрирует грид в UIRegistry
            _inventoryGrid.BindToEntity(inventoryEntity);
        }

        private void ShowWindow()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
        }

        private void CloseWindow()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }

            // ✅ Отписываемся от реестра при закрытии
            if (_inventoryGrid != null && _inventoryGrid.InventoryEntity != Entity.Null)
            {
                UIRegistry.Unregister(_inventoryGrid.InventoryEntity, _inventoryGrid);
            }
        }
    }
}

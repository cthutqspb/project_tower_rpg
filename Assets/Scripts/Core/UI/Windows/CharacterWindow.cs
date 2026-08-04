using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class CharacterWindow : UIWindow
    {
        private HeaderComponent _header;
        private StaticGrid _inventoryGrid;

        // ================================================================
        // ПОДПИСКА НА ХОТКЕИ
        // ================================================================

        private void OnEnable()
        {
            UIEvents.ToggleCharacterWindow += Toggle;
        }

        private void OnDisable()
        {
            UIEvents.ToggleCharacterWindow -= Toggle;
        }

        // ================================================================
        // СБОРКА ОКНА
        // ================================================================

        protected override void OnWindowBuilt(VisualElement root)
        {
            // Хедер
            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                _header = new HeaderComponent();
                _header.Title = "РЮКЗАК ПЕРСОНАЖА";
                _header.OnClose += Close;
                headerContainer.Add(_header);

                var dragManipulator = new DragManipulator(
                    dragElement: _header,
                    targetElement: root,
                    mode: DragMode.UIElement
                );
                _header.AddManipulator(dragManipulator);
            }

            // Инвентарь
            var inventoryContainer = root.Q<VisualElement>("inventory-container");
            if (inventoryContainer != null)
            {
                _inventoryGrid = new StaticGrid(columns: 6, rows: 4, gridType: "inventory");
                _inventoryGrid.DataSourceId = "unit_inventory";
                inventoryContainer.Add(_inventoryGrid);
                LinkInventory();
            }

            // Кукла
            var paperdollContainer = root.Q<VisualElement>("paperdoll-container");
            if (paperdollContainer != null)
            {
                // TODO: PaperdollComponent
            }
        }

        // ================================================================
        // ИНИЦИАЛИЗАЦИЯ
        // ================================================================

        private void LinkInventory()
        {
            if (World.DefaultGameObjectInjectionWorld == null) return;

            var inventoryEntity = EntityRegistry.Get("unit_inventory");
            if (inventoryEntity == Entity.Null)
            {
                Debug.LogWarning("[CharacterWindow] Сущность инвентаря ещё не создана");
                return;
            }

            _inventoryGrid.BindToEntity(inventoryEntity);
        }

        // ================================================================
        // КОЛЛБЭКИ
        // ================================================================

        protected override void OnWindowShown()
        {
            //_inventoryGrid?.Refresh();
            Debug.Log("[CharacterWindow] Показано");
        }

        protected override void OnWindowClosed()
        {
            if (_inventoryGrid != null && _inventoryGrid.InventoryEntity != Entity.Null)
            {
                UIRegistry.Unregister(_inventoryGrid.InventoryEntity, _inventoryGrid);
            }
            Debug.Log("[CharacterWindow] Закрыто");
        }
    }
}

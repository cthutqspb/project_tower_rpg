using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class CharacterWindow : UIWindow
    {
        [Header("UI Components")]
        [SerializeField] private VisualTreeAsset _paperdollUxml;

        private HeaderComponent _header;
        private StaticGrid _inventoryGrid;
        private Paperdoll _paperdoll;

        private void OnEnable()
        {
            UIEvents.ToggleCharacterWindow += Toggle;
        }

        private void OnDisable()
        {
            UIEvents.ToggleCharacterWindow -= Toggle;
        }

        // ЗДЕСЬ МЫ СТРОИМ РАЗМЕТКУ И СРАЗУ СВЯЗЫВАЕМ ЕЁ С СУЩНОСТЯМИ
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
            var inventoryEntity = EntityRegistry.Get("unit_inventory"); // ИСПРАВЛЕНО: Вынесли выше проверки
            
            if (inventoryContainer != null && inventoryEntity != Entity.Null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    var inventoryComp = world.EntityManager.GetComponentData<ContainerConfigComponent>(inventoryEntity);
                    _inventoryGrid = new StaticGrid(inventoryComp.Columns, inventoryComp.Rows, "inventory");
                    _inventoryGrid.DataSourceId = "unit_inventory";
                    
                    // ЖЕЛЕЗНО ПРИВЯЗЫВАЕМ СУЩНОСТЬ: Теперь BoundEntity внутри сетки НЕ будет равен Entity.Null!
                    _inventoryGrid.BindToEntity(inventoryEntity); 
                    
                    inventoryContainer.Add(_inventoryGrid);
                }
            }

            // Кукла
            var paperdollContainer = root.Q<VisualElement>("paperdoll-container");
            var paperdollEntity = EntityRegistry.Get("unit_paperdoll"); // ИСПРАВЛЕНО: Вынесли выше проверки
            
            if (paperdollContainer != null && paperdollEntity != Entity.Null)
            {
                _paperdoll = new Paperdoll(_paperdollUxml);
                _paperdoll.DataSourceId = "unit_paperdoll";
                
                // ЖЕЛЕЗНО ПРИВЯЗЫВАЕМ СУЩНОСТЬ: Теперь BoundEntity внутри куклы НЕ будет равен Entity.Null!
                _paperdoll.BindToEntity(paperdollEntity); 
                
                paperdollContainer.Add(_paperdoll);
            }
        }
    }
}


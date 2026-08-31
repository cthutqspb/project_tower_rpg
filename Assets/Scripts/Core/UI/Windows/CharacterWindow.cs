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
        [SerializeField] private VisualTreeAsset _statsUxml;

        private HeaderComponent _header;
        private StaticGrid _inventoryGrid;
        private Paperdoll _paperdoll;
        private UnitStats _unitStats;

        private void OnEnable()
        {
            UIEvents.ToggleCharacterWindow += Toggle;
        }

        private void OnDisable()
        {
            UIEvents.ToggleCharacterWindow -= Toggle;
        }

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

                var dragManipulator = new DragManipulator(dragElement: _header, targetElement: root, mode: DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }

            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // ================================================================
            // 📊 ИНИЦИАЛИЗАЦИЯ И ОТРИСОВКА СТАТОВ И АТРИБУТОВ ИГРОКА
            // ================================================================
            var statsContainer = root.Q<VisualElement>("stats-container");
            if (statsContainer != null && _statsUxml != null)
            {
                // Создаем компонент характеристик, скармливая ему ассет шаблона статов
                _unitStats = new UnitStats(_statsUxml);
                
                // Привязываем к сущности игрока: он СРАЗУ вытянет стартовые Атрибуты из ECS!
                _unitStats.BindToEntity(playerEntity);
                
                // Шёлково добавляем готовый элемент в левую колонку окна
                statsContainer.Add(_unitStats);
                UIRegistry.Register(playerEntity, _unitStats);
            }

            // Инвентарь
            var inventoryEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(playerEntity, em);
            var inventoryContainer = root.Q<VisualElement>("inventory-container");
            
            if (inventoryContainer != null && inventoryEntity != Entity.Null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    var inventoryComp = world.EntityManager.GetComponentData<ContainerConfigComponent>(inventoryEntity);
                    _inventoryGrid = new StaticGrid(inventoryComp.Columns, inventoryComp.Rows);
                    
                    _inventoryGrid.BindToEntity(inventoryEntity); 
                    inventoryContainer.Add(_inventoryGrid);
                }
            }

            // Кукла
            var paperdollEntity = ContainerHelper.GetContainerForUnit<PaperdollTag>(playerEntity, em);
            var paperdollContainer = root.Q<VisualElement>("paperdoll-container");
            
            if (paperdollContainer != null && paperdollEntity != Entity.Null)
            {
                _paperdoll = new Paperdoll(_paperdollUxml);
                
                _paperdoll.BindToEntity(paperdollEntity); 
                paperdollContainer.Add(_paperdoll);
            }
        }
    }
}

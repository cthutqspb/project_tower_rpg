using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class ContainerWindow : UIWindow, IEntityContainer
    {
        private HeaderComponent _header;
        private StaticGrid _lootGrid;
        private Button _takeAllButton;
        private Entity _containerEntity = Entity.Null;
        private bool _isWaitingForBag = false;
        private VisualElement _inventoryContainer;

        public Entity BoundEntity => _containerEntity;

        private void OnEnable()
        {
            UIEvents.OpenContainerWindow += OpenContainer;
        }

        private void OnDisable()
        {
            UIEvents.OpenContainerWindow -= OpenContainer;
        }

        public void OpenContainer(Entity targetContainerEntity)
        {
            _containerEntity = targetContainerEntity;
            _isWaitingForBag = true;
            Toggle();
        }

        protected override void OnWindowBuilt(VisualElement root)
        {
            // Хедер
            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                _header = new HeaderComponent();
                _header.Title = "ДОБЫЧА";
                _header.OnClose += Close;
                headerContainer.Add(_header);

                var dragManipulator = new DragManipulator(dragElement: _header, targetElement: root, mode: DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }

            _takeAllButton = root.Q<Button>("take-all-btn");
            if (_takeAllButton != null)
            {
                _takeAllButton.clicked += OnTakeAllClicked;
            }

            _inventoryContainer = root.Q<VisualElement>("inventory-container");
            if (_inventoryContainer != null && _containerEntity != Entity.Null)
            {
                TryBuildGrid();
            }
        }

        private void Update()
        {
            // Если окно открыто и мы ждём мешок — пробуем построить сетку каждый кадр
            if (_isWaitingForBag && _inventoryContainer != null && _containerEntity != Entity.Null)
            {
                TryBuildGrid();
            }
        }

        private void TryBuildGrid()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity lootBagEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(_containerEntity, em);

            if (lootBagEntity != Entity.Null)
            {
                // Мешок найден — строим сетку
                var containerComp = em.GetComponentData<ContainerConfigComponent>(lootBagEntity);
                _lootGrid = new StaticGrid(containerComp.Columns, containerComp.Rows, "loot");
                _lootGrid.BindToEntity(lootBagEntity);
                _inventoryContainer.Add(_lootGrid);
                
                _isWaitingForBag = false;
                Debug.Log($"📦 [ContainerWindow] Сетка создана для мешка {lootBagEntity.Index}");
            }
            // else — ждём следующий кадр, ECB ещё не применился
        }

        private void OnTakeAllClicked()
        {
            if (_containerEntity == Entity.Null) return;
            Debug.Log($"💰 [UI] 'ВЗЯТЬ ВСЁ' для сундука {_containerEntity.Index}");
            Close();
        }
    }
}

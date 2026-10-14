using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class CharacterSheet
    {
        private readonly VisualElement _pageRoot;
        private readonly VisualElement _inventorySlot;

        private UnitStats _unitStats;
        private Paperdoll _paperdoll;
        private StaticGrid _inventoryGrid;

        private Entity _playerEntity = Entity.Null;

        // Конструктор — только создание VE, без бинда
        public CharacterSheet(
            VisualElement pageRoot,
            VisualTreeAsset statsUxml,
            VisualTreeAsset paperdollUxml)
        {
            _pageRoot = pageRoot;
            if (_pageRoot == null) return;

            var statsSlot = _pageRoot.Q<VisualElement>("stats");
            var paperdollSlot = _pageRoot.Q<VisualElement>("paperdoll");
            _inventorySlot = _pageRoot.Q<VisualElement>("inventory");

            if (statsSlot != null && statsUxml != null)
            {
                _unitStats = new UnitStats(statsUxml);
                statsSlot.Add(_unitStats);
            }

            if (paperdollSlot != null && paperdollUxml != null)
            {
                _paperdoll = new Paperdoll(paperdollUxml);
                paperdollSlot.Add(_paperdoll);
            }

            // StaticGrid (инвентарь) — создаём СРАЗУ с дефолтным размером.
            // При первом BindToEntity подгоним через ResizeAndExpand под реальные columns/rows.
            if (_inventorySlot != null)
            {
                _inventoryGrid = new StaticGrid(6, 12);
                _inventoryGrid.AddToClassList("character-window__inventory-grid");
                _inventorySlot.Add(_inventoryGrid);
            }
        }

        // Бинд — регистрация в UIRegistry и подтягивание данных из ECS
        public void BindToEntity(Entity playerEntity)
        {
            if (playerEntity == Entity.Null) return;
            _playerEntity = playerEntity;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (_unitStats != null)
                _unitStats.BindToEntity(_playerEntity);

            if (!em.HasComponent<BuffersLinkComponent>(_playerEntity)) return;
            var links = em.GetComponentData<BuffersLinkComponent>(_playerEntity);

            if (_paperdoll != null && links.Paperdoll != Entity.Null)
                _paperdoll.BindToEntity(links.Paperdoll);

            if (_inventoryGrid != null && links.Inventory != Entity.Null)
            {
                var inventoryComp = em.GetComponentData<ContainerConfigComponent>(links.Inventory);
                _inventoryGrid.ResizeAndExpand(inventoryComp.Columns, inventoryComp.Rows);
                _inventoryGrid.BindToEntity(links.Inventory);
            }
        }

        public void Unbind()
        {
            if (_unitStats != null && _playerEntity != Entity.Null)
                UIRegistry.Unregister(_playerEntity, _unitStats);

            if (_inventoryGrid != null && _inventoryGrid.BoundEntity != Entity.Null)
                UIRegistry.Unregister(_inventoryGrid.BoundEntity, _inventoryGrid);

            if (_paperdoll != null && _paperdoll.BoundEntity != Entity.Null)
                UIRegistry.Unregister(_paperdoll.BoundEntity, _paperdoll);

            _playerEntity = Entity.Null;
        }
    }
}

using System.Collections.Generic;
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

            // ================================================================
            // 1. ЗАГРУЖАЕМ КАРКАС
            // ================================================================
            _root = _windowUxml.CloneTree();
            _root.pickingMode = PickingMode.Position;
            globalUiRoot.Add(_root);

            // ================================================================
            // 2. ХЕДЕР
            // ================================================================
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

            // ================================================================
            // 3. ИНВЕНТАРЬ
            // ================================================================
            var inventoryContainer = _root.Q<VisualElement>("inventory-container");
            if (inventoryContainer != null)
            {
                _inventoryGrid = new StaticGrid(columns: 6, rows: 4, gridType: "inventory");
                
                // DataSourceId — это строковый идентификатор, который будет улетать в DragManager.
                // Передаем туда точное название ключа регистрации сетки.
                _inventoryGrid.DataSourceId = "unit_inventory";
                inventoryContainer.Add(_inventoryGrid);

                // Регистрируем и принудительно инициализируем UI актуальными ECS-данными
                LinkAndInitializeInventory();
            }

            // ================================================================
            // 4. КУКЛА (задел)
            // ================================================================
            var paperdollContainer = _root.Q<VisualElement>("paperdoll-container");
            if (paperdollContainer != null)
            {
                // TODO: PaperdollComponent
                // var paperdoll = new PaperdollComponent();
                // paperdollContainer.Add(paperdoll);
            }

            // ================================================================
            // 5. ПОКАЗЫВАЕМ ОКНО
            // ================================================================
            ShowWindow();
            
            Debug.Log("🎯 [CharacterWindow]: Окно собрано из компонентов!");
        }

        private void LinkAndInitializeInventory()
        {
            if (World.DefaultGameObjectInjectionWorld == null) return;

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // "unit_inventory" — это сущность САМОЙ СЕТКИ инвентаря, 
            // которая была создана при спавне и зарегистрирована в реестре.
            var inventoryEntity = EntityRegistry.Get("unit_inventory");

            if (inventoryEntity == Entity.Null)
            {
                Debug.LogWarning("[CharacterWindow] Сущность инвентаря ещё не создана в ECS/реестре");
                return;
            }

            // Связываем ECS Сущность инвентаря с UI экземпляром в нашем статическом реестре сеток.
            // Теперь SyncAllGridsUiSystem сможет мгновенно находить это окно!
            EntityRegistry.RegisterGrid(inventoryEntity, _inventoryGrid);

            // ПЕРВИЧНАЯ ЗАЛИВКА ДАННЫХ ПРИ ОТКРЫТИИ ОКНА:
            // Чтобы не ждать следующего перемещения предмета для отрисовки,
            // принудительно забираем текущее состояние буфера и заливаем в слоты.
            if (entityManager.HasBuffer<SlotData>(inventoryEntity))
            {
                var slots = entityManager.GetBuffer<SlotData>(inventoryEntity);
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    _inventoryGrid.UpdateSlot(
                        slot.SlotIndex,
                        slot.DataId.ToString(),
                        slot.DataType.ToString(),
                        slot.Amount,
                        slot.ContainerType.ToString()
                    );
                }
            }
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

            // Разрываем связь в реестре, чтобы избежать утечек памяти при уничтожении UI окна
            var inventoryEntity = EntityRegistry.Get("unit_inventory");
            if (inventoryEntity != Entity.Null)
            {
                EntityRegistry.UnregisterGrid(inventoryEntity);
            }
        }
    }
}


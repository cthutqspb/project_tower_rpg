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

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("🚨 UI ДЕБАГ: Мир ECS равен null!");
                return;
            }
            var em = world.EntityManager;

            // Находим сущность самого Игрока по тегу PlayerTag
            var playerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            if (playerQuery.IsEmpty)
            {
                Debug.LogError("🚨 UI ДЕБАГ: Сущность с PlayerTag не найдена в ECS!");
                return;
            }
            var playerEntity = playerQuery.GetSingletonEntity();
            Debug.Log($"🎯 UI ДЕБАГ: Найдена сущность игрока: {playerEntity}");

            // Стерильно заявляем пустые сущности под контейнеры игрока
            Entity inventoryEntity = Entity.Null;
            Entity paperdollEntity = Entity.Null;

            // Сканируем все контейнеры в мире
            var containerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ContainerConfigComponent>());
            var containers = containerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            Debug.Log($"📦 UI ДЕБАГ: Всего контейнеров в ECS-памяти: {containers.Length}");

            foreach (var container in containers)
            {
                var config = em.GetComponentData<ContainerConfigComponent>(container);
                Debug.Log($"🔍 UI ДЕБАГ: Проверяем контейнер {container}. Владелец в конфиге: {config.Owner}. Наш игрок: {playerEntity}");
                
                if (config.Owner == playerEntity)
                {
                    if (config.Rows == 1)
                    {
                        paperdollEntity = container;
                    }
                    else
                    {
                        inventoryEntity = container;
                    }
                }
            }
            containers.Dispose();

            Debug.Log($"📊 UI ДЕБАГ: Финальные сущности -> Инвентарь: {inventoryEntity}, Кукла: {paperdollEntity}");

            // ================================================================
            // 🎒 ИНВЕНТАРЬ
            // ================================================================
            var inventoryContainer = root.Q<VisualElement>("inventory-container");
            if (inventoryContainer == null) Debug.LogError("🚨 UI ДЕБАГ: inventory-container НЕ НАЙДЕН в UXML-разметке!");
            
            if (inventoryContainer != null && inventoryEntity != Entity.Null)
            {
                var inventoryComp = em.GetComponentData<ContainerConfigComponent>(inventoryEntity);
                _inventoryGrid = new StaticGrid(inventoryComp.Columns, inventoryComp.Rows, "inventory");
                _inventoryGrid.DataSourceId = "player_inventory";
                _inventoryGrid.BindToEntity(inventoryEntity); 
                inventoryContainer.Add(_inventoryGrid);
                Debug.Log("✅ UI ДЕБАГ: Сетка инвентаря УСПЕШНО добавлена на экран!");
            }

            // ================================================================
            // 👕 КУКЛА ШМОТА
            // ================================================================
            var paperdollContainer = root.Q<VisualElement>("paperdoll-container");
            if (paperdollContainer == null) Debug.LogError("🚨 UI ДЕБАГ: paperdoll-container НЕ НАЙДЕН в UXML-разметке!");
            
            if (paperdollContainer != null && paperdollEntity != Entity.Null)
            {
                _paperdoll = new Paperdoll(_paperdollUxml);
                _paperdoll.DataSourceId = "player_paperdoll";
                _paperdoll.BindToEntity(paperdollEntity); 
                paperdollContainer.Add(_paperdoll);
                Debug.Log("✅ UI ДЕБАГ: Кукла шмота УСПЕШНО добавлена на экран!");
            }
        }
   }
}


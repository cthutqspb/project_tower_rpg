using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class ContainerWindow : UIWindow, IEntityContainer
    {
        // 🦾 Фиксируем максимальный MMO-размер сетки добычи (например, 4 столбца на 4 строки)
        private const int MAX_LOOT_COLUMNS = 4;
        private const int MAX_LOOT_ROWS = 4;

        private HeaderComponent _header;
        private StaticGrid _lootGrid;
        private Button _takeAllButton;
        private Entity _containerEntity = Entity.Null;
        private bool _isWaitingForBag = false;
        private VisualElement _inventoryContainer;

        public override WindowType Type => WindowType.Container;
        public override Entity BoundEntity => _containerEntity;

        private void OnEnable()
        {
            UIEvents.CloseAllWindows += Close;
        }

        private void OnDisable()
        {
            UIEvents.CloseAllWindows -= Close;
        }

        // =========================================================================
        // 🦾 ШАГ 1: ТЯЖЕЛАЯ СБОРКА СЕТКИ ПЕРЕНЕСЕНА В AWAKE (Происходит скрыто при спавне!)
        // =========================================================================
        protected override void OnWindowBuilt(VisualElement root)
        {
            // Хедер собираем сразу
            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                _header = new HeaderComponent { Title = "ДОБЫЧА" };
                _header.OnClose += Close;
                headerContainer.Add(_header);
                root.RegisterCallback<PointerDownEvent>(evt => root.BringToFront());
                var dragManipulator = new DragManipulator(_header, root, DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }

            _takeAllButton = root.Q<Button>("take-all-btn");
            if (_takeAllButton != null) _takeAllButton.clicked += OnTakeAllClicked;

            _inventoryContainer = root.Q<VisualElement>("inventory-container");
            
            // 🎯 СТРОИМ ГРИД СРАЗУ ПРИ РОЖДЕНИИ ПРЕФАБА!
            // Ему пока не нужна сущность, мы просто генерируем меш слотов в ОЗУ.
            if (_inventoryContainer != null && _lootGrid == null)
            {
                _lootGrid = new StaticGrid(MAX_LOOT_COLUMNS, MAX_LOOT_ROWS);
                _inventoryContainer.Add(_lootGrid);
                
                // Кэшируем ресиверы в базовом классе UIWindow
                ScanAndCacheReceivers();
                Debug.Log("📦 [ContainerWindow.OnWindowBuilt]: Максимальная сетка лута 4х4 аппаратно сгенерирована в ОЗУ.");
            }
        }

        public override void Setup(Entity targetContainerEntity)
        {
            _containerEntity = targetContainerEntity;
            _isWaitingForBag = true;
            if (_root != null) _root.userData = _containerEntity;
            
            // Пробуем привязать данные сразу, если мешок уже готов в ECS
            TryBuildGrid();
            Open();
        }

        public override void Close()
        {
            UnbindContainerData();
            base.Close();
        }

        private void UnbindContainerData()
        {
            if (_lootGrid != null && _lootGrid.BoundEntity != Entity.Null)
            {
                UIRegistry.Unregister(_lootGrid.BoundEntity, _lootGrid);
                // ❌ Больше НЕ зануляем и НЕ удаляем _lootGrid! Он остается спать внутри префаба.
            }

            _containerEntity = Entity.Null;
            _isWaitingForBag = false;
            if (_root != null) _root.userData = null;
        }

        private void Update()
        {
            if (_isWaitingForBag && _inventoryContainer != null && _containerEntity != Entity.Null) 
                TryBuildGrid();
        }

        // =========================================================================
        // 🦾 ШАГ 2: МГНОВЕННАЯ ПЕРЕПРИВЯЗКА ЗА 0 НАНОСЕКУНД (Без фризов рендеринга!)
        // =========================================================================
        private void TryBuildGrid()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (_containerEntity == Entity.Null || !em.Exists(_containerEntity) || !em.HasComponent<BuffersLinkComponent>(_containerEntity)) return;

            var links = em.GetComponentData<BuffersLinkComponent>(_containerEntity);
            Entity lootBagEntity = links.Inventory;
            if (lootBagEntity == Entity.Null || !em.Exists(lootBagEntity)) return;

            // Если эта сетка уже шёлково обслуживает текущий мешок — выходим
            if (_lootGrid != null && _lootGrid.BoundEntity == lootBagEntity)
            {
                _isWaitingForBag = false;
                return;
            }

            if (_lootGrid != null)
            {
                // 1. Если сетка держала старый сундук — выписываем его из покадрового UI-реестра
                if (_lootGrid.BoundEntity != Entity.Null)
                {
                    UIRegistry.Unregister(_lootGrid.BoundEntity, _lootGrid);
                }

                // 2. БЕСШОВНЫЙ REBIND: Скармливаем сетке новый ID мешка.
                // Внутри .BindToEntity() у тебя уже зашит вызов UIRegistry.Register, так что связь восстановится сама!
                _lootGrid.BindToEntity(lootBagEntity);

                // 3. 🦾 УМНАЯ АДАПТАЦИЯ (WoW-канон):
                // Передаем команду в StaticGrid. Он шёлково настроит display: None для лишних ячеек,
                // а если сундук окажется аномально большим (больше стартовых 4х6) — сам дорастит пул слотов в ОЗУ!
                var containerComp = em.GetComponentData<ContainerConfigComponent>(lootBagEntity);
                _lootGrid.ResizeAndExpand(containerComp.Columns, containerComp.Rows);

                // 4. Актуализируем кэш ресиверов базового класса UIWindow под новые реалии
                ScanAndCacheReceivers();

                // 5. Форсируем чтение шмоток, чтобы экран обновился мгновенно, не дожидаясь тика систем
                if (em.HasBuffer<ItemSlot>(lootBagEntity))
                {
                    _lootGrid.UpdateFromBuffer(em.GetBuffer<ItemSlot>(lootBagEntity));
                }

                _isWaitingForBag = false;
                Debug.Log($"🔄 [ContainerWindow]: Бесшовная перепривязка готовой сетки к сундуку {lootBagEntity.Index}. 0 фризов!");
            }
        }

        private void OnTakeAllClicked()
        {   
            if (_containerEntity == Entity.Null) return;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity activePlayerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            Entity commandEntity = em.CreateEntity();
            em.AddComponentData(commandEntity, new ActionCommand { Action = ContainerActions.TakeAll, SourceEntity = activePlayerEntity, TargetEntity = _containerEntity });
        }
    }
}


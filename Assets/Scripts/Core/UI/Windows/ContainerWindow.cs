using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;

namespace ProjectTowerRpg.Core.UI.Windows
{
    public class ContainerWindow : UIWindow
    {
        private const int MAX_LOOT_COLUMNS = 4;
        private const int MAX_LOOT_ROWS = 4;

        private HeaderComponent _header;
        private StaticGrid _lootGrid;
        private Button _takeAllButton;
        private Entity _containerEntity = Entity.Null;
        private bool _isWaitingForBag = false;
        private bool _waitForEmptyToClose = false; // 🎯 Новый флаг
        private VisualElement _gridSlot;

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

        protected override void OnWindowBuilt(VisualElement root)
        {
            var headerSlot = root.Q<VisualElement>("header");
            if (headerSlot != null)
            {
                _header = new HeaderComponent { Title = "ДОБЫЧА" };
                _header.OnClose += Close;
                headerSlot.Add(_header);
                root.RegisterCallback<PointerDownEvent>(evt => root.BringToFront());
                var dragManipulator = new DragManipulator(_header, _window, DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }

            _takeAllButton = root.Q<Button>("take-all-btn");
            if (_takeAllButton != null) _takeAllButton.clicked += OnTakeAllClicked;

            _gridSlot = root.Q<VisualElement>("grid");
            
            if (_gridSlot != null && _lootGrid == null)
            {
                _lootGrid = new StaticGrid(MAX_LOOT_COLUMNS, MAX_LOOT_ROWS);
                _lootGrid.AddToClassList("container-window__grid");
                _gridSlot.Add(_lootGrid);
                              
                ScanAndCacheReceivers();                
            }
        }

        public override void Setup(Entity targetContainerEntity)
        {
            _containerEntity = targetContainerEntity;
            _isWaitingForBag = true;
            _waitForEmptyToClose = false; // 🎯 Сбрасываем флаг при открытии
            if (_root != null) _root.userData = _containerEntity;
            
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
            }

            _containerEntity = Entity.Null;
            _isWaitingForBag = false;
            _waitForEmptyToClose = false; // 🎯 Сбрасываем флаг
            if (_root != null) _root.userData = null;
        }

        private void Update()
        {
            if (_isWaitingForBag && _gridSlot != null && _containerEntity != Entity.Null) 
                TryBuildGrid();

            // 🎯 Проверка на автозакрытие после "Взять всё"
            if (_waitForEmptyToClose && _containerEntity != Entity.Null)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (em.Exists(_containerEntity) && em.HasComponent<BuffersLinkComponent>(_containerEntity))
                {
                    var links = em.GetComponentData<BuffersLinkComponent>(_containerEntity);
                    Entity lootBagEntity = links.Inventory;
                    if (em.Exists(lootBagEntity) && em.HasBuffer<ItemSlot>(lootBagEntity))
                    {
                        var slots = em.GetBuffer<ItemSlot>(lootBagEntity);
                        bool isEmpty = true;
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (!slots[i].IsEmpty) { isEmpty = false; break; }
                        }

                        if (isEmpty)
                        {
                            _waitForEmptyToClose = false;
                            // 🎯 Возвращаем в пул через фабрику, чтобы окно переиспользовалось, а не висело в памяти
                            if (WindowFactory.Instance != null)
                                WindowFactory.Instance.ReturnToPool(Type, this);
                            else
                                Close(); // fallback
                        }
                    }
                }
            }
        }

        private void TryBuildGrid()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (_containerEntity == Entity.Null || !em.Exists(_containerEntity) || !em.HasComponent<BuffersLinkComponent>(_containerEntity)) return;

            var links = em.GetComponentData<BuffersLinkComponent>(_containerEntity);
            Entity lootBagEntity = links.Inventory;
            if (lootBagEntity == Entity.Null || !em.Exists(lootBagEntity)) return;

            if (_lootGrid != null)
            {
                // 🎯 Убрали ранний return, чтобы при переоткрытии окна сетка заново привязалась к сущности
                if (_lootGrid.BoundEntity != Entity.Null)
                {
                    UIRegistry.Unregister(_lootGrid.BoundEntity, _lootGrid);
                }

                _lootGrid.BindToEntity(lootBagEntity);

                var containerComp = em.GetComponentData<ContainerConfigComponent>(lootBagEntity);
                _lootGrid.ResizeAndExpand(containerComp.Columns, containerComp.Rows);

                ScanAndCacheReceivers();

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

            _waitForEmptyToClose = true; // 🎯 Включаем ожидание закрытия
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Localization;

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
        
        private Entity _playerEntity = Entity.Null;
        private bool _isContentBuilt = false; // 🦾 Флаг, гарантирующий ОДНОКРАТНУЮ сборку веток

        public override WindowType Type => WindowType.Character;
        public override Entity BoundEntity => _playerEntity;

        private void OnEnable()
        {
            UIEvents.OnCloseWindow += OnCloseWindowRequested;
            UIEvents.CloseAllWindows += Close;
        }

        private void OnDisable()
        {
            UIEvents.OnCloseWindow -= OnCloseWindowRequested;
            UIEvents.CloseAllWindows -= Close;
        }

        public override void Setup(Entity entity)
        {
            _playerEntity = entity != Entity.Null ? entity : PlayerUtils.GetEntityByTag<PlayerTag>();
            
            if (_root != null) _root.userData = _playerEntity;

            // 🦾 ПРАВИЛЬНЫЙ ТАЙМИНГ: Строим тяжелый визуал ОДИН раз за всю жизнь префаба!
            if (!_isContentBuilt && _root != null && _playerEntity != Entity.Null)
            {
                BuildWindowContent(_root);
            }
            else if (_isContentBuilt && _playerEntity != Entity.Null)
            {
                // Если верстка уже готова — просто "переподвязываем" живые сетки к актуальной сущности за 0 наносекунд!
                RebindExistingContent();
            }

            Open(); 
        }

        private void OnCloseWindowRequested(WindowType type, Entity entity)
        {
            if (type == WindowType.Character && _playerEntity == entity) Close();
        }

        public override void Close()
        {
            Unbind(); // Просто отвязываем данные, верстку не трогаем!
            base.Close();
        }

        public override void Unbind()
        {
            // 🦾 МАКСИМАЛЬНАЯ СТЕРfieldsННОСТЬ: Выписываем компоненты из памяти UIRegistry,
            // чтобы UIPullSystem перестала покадрово пушить в них данные...
            if (_unitStats != null && _playerEntity != Entity.Null) UIRegistry.Unregister(_playerEntity, _unitStats);
            if (_inventoryGrid != null) UIRegistry.Unregister(_inventoryGrid.BoundEntity, _inventoryGrid);
            if (_paperdoll != null) UIRegistry.Unregister(_paperdoll.BoundEntity, _paperdoll);

            _playerEntity = Entity.Null;
            if (_root != null) _root.userData = null;
            
            // ❌ УБРАЛИ ОТСЮДА УНИЧТОЖЕНИЕ И ЗАНУЛЕНИЕ СЕТОК! Сетки остаются спать в памяти префаба.
        }

        protected override void OnWindowBuilt(VisualElement root)
        {
            // Хедер собираем сразу — он статичен
            var headerSlot = root.Q<VisualElement>("header");
            if (headerSlot != null)
            {
                _header = new HeaderComponent { Title = LocalizationManager.Get("character_window") };
                _header.OnClose += Close;
                headerSlot.Add(_header);
                root.RegisterCallback<PointerDownEvent>(evt => root.BringToFront());
                var dragManipulator = new DragManipulator(_header, root, DragMode.UIElement);
                _header.AddManipulator(dragManipulator);
            }
        }

        private void BuildWindowContent(VisualElement root)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // ================================================================
            // 📊 ХАРАКТЕРИСТИКИ (Создаем ОДИН раз)
            // ================================================================
            var statsSlot = root.Q<VisualElement>("stats");
            if (statsSlot != null && _statsUxml != null)
            {
                _unitStats = new UnitStats(_statsUxml);
                _unitStats.BindToEntity(_playerEntity);
                statsSlot.Add(_unitStats);
                UIRegistry.Register(_playerEntity, _unitStats);
            }

            // ================================================================
            // 🎒 КАРМАНЫ (Инвентарь и Кукла создаются ОДИН раз)
            // ================================================================
            if (em.HasComponent<BuffersLinkComponent>(_playerEntity))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(_playerEntity);
                Entity inventoryEntity = links.Inventory;
                Entity paperdollEntity = links.Paperdoll;

                var inventorySlot = root.Q<VisualElement>("inventory");
                if (inventorySlot != null && inventoryEntity != Entity.Null)
                {
                    var inventoryComp = em.GetComponentData<ContainerConfigComponent>(inventoryEntity);
                    _inventoryGrid = new StaticGrid(inventoryComp.Columns, inventoryComp.Rows);
                    _inventoryGrid.BindToEntity(inventoryEntity);
                    _inventoryGrid.AddToClassList("character-window__inventory-grid");
                    inventorySlot.Add(_inventoryGrid);
                }

                var paperdollSlot = root.Q<VisualElement>("paperdoll");
                if (paperdollSlot != null && paperdollEntity != Entity.Null)
                {
                    _paperdoll = new Paperdoll(_paperdollUxml);
                    _paperdoll.BindToEntity(paperdollEntity); 
                    paperdollSlot.Add(_paperdoll);
                }
            }

            ScanAndCacheReceivers();
            _isContentBuilt = true; // Фиксируем: окно полностью собрано в ОЗУ
        }

        /// <summary>
        /// Быстрый Си-метод перепривязки живых компонентов к новой/старой сущности при взятии из пула
        /// </summary>
        private void RebindExistingContent()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (_unitStats != null)
            {
                _unitStats.BindToEntity(_playerEntity);
                UIRegistry.Register(_playerEntity, _unitStats);
            }

            if (em.HasComponent<BuffersLinkComponent>(_playerEntity))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(_playerEntity);
                
                if (_inventoryGrid != null && links.Inventory != Entity.Null)
                {
                    _inventoryGrid.BindToEntity(links.Inventory);
                }

                if (_paperdoll != null && links.Paperdoll != Entity.Null)
                {
                    _paperdoll.BindToEntity(links.Paperdoll);
                }
            }

            // Перерегистрируем проснувшиеся живые ресиверы в UIRegistry
            ScanAndCacheReceivers();
        }
    }
}


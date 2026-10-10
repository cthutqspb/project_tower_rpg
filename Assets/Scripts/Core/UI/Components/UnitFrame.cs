using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.ECS.Components; // Твой namespace с компонентами

namespace ProjectTowerRpg.Core.UI.Components
{
    [Serializable]
    // 🌟 ИСПРАВЛЕНО: Теперь класс официально подписывает контракт ресивера компонентов ECS!
    public class UnitFrame : IEcsUiComponentReceiver<HealthComponent>,
                             IEcsUiComponentReceiver<ResourceComponent>,
                             IEcsUiComponentReceiver<UnitComponent>
    {
        [Header("Список компонентов фрейма")]
        [SerializeField] private VisualTreeAsset _frameUxml;       // Сюда UnitFrame.uxml
        [SerializeField] private VisualTreeAsset _progressBarUxml; // Сюда ProgressBar.uxml
        [SerializeField] private VisualTreeAsset _auraFrameUxml;
        //public bool IsTargetFrame = false;

        private VisualElement _frameRoot;
        private Label _unitNameLabel;
        private Label _unitLevelLabel;
        
        private HealthBar _unitHealthBar;
        private ResourceBar _unitResourceBar;
        private AuraFrame _unitAuraFrame;

        public UnitFrame() { }
        
        private Entity _boundEntity = Entity.Null;

        public AuraFrame UnitAuraFrame => _unitAuraFrame;
       
        /// <summary>
        /// Сборка фрейма прямо внутри переданного слота
        /// </summary>
        public void BuildFrame(VisualElement slotContainer)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Метод BuildFrame запущен для слота: '{slotContainer?.name ?? "NULL"}'");

            if (slotContainer == null || _frameUxml == null) 
            {
                Debug.LogError($"🚨 [UnitFrame ДЕБАГ]: Ошибка сборки! slotContainer={slotContainer != null}, _frameUxml={_frameUxml != null}");
                return;
            }

            // Защита от дублирования при перезагрузке UI в редакторе
            if (_frameRoot != null && slotContainer.Contains(_frameRoot))
            {
                Debug.Log("[UnitFrame ДЕБАГ]: Удаляю старую вёрстку из слота перед пересборкой.");
                slotContainer.Remove(_frameRoot);
            }

            // Клонируем каркас фрейма прямо внутрь слота
            _frameRoot = _frameUxml.CloneTree();
            slotContainer.Add(_frameRoot);
            _frameRoot.pickingMode = PickingMode.Ignore;

            // Находим внутренние ноды текста и полосок
            var healthSlot = _frameRoot.Q<VisualElement>("health");
            var resourceSlot = _frameRoot.Q<VisualElement>("resource");
            
            var aurasSlot = _frameRoot.Q<VisualElement>("auras");
            aurasSlot.pickingMode = PickingMode.Ignore;

            _unitNameLabel = _frameRoot.Q<Label>("unit-name");
            _unitLevelLabel = _frameRoot.Q<Label>("unit-level");

            Debug.Log($"[UnitFrame ДЕБАГ]: Поиск внутренних нод: hpRoot={(healthSlot != null)}, resRoot={(resourceSlot != null)}, name={(_unitNameLabel != null)}, level={(_unitLevelLabel != null)}");

            // Вживляем внутренние полоски из списка компонентов фрейма
            if (_progressBarUxml != null)
            {
                healthSlot?.Add(_progressBarUxml.CloneTree());
                resourceSlot?.Add(_progressBarUxml.CloneTree());
                Debug.Log("[UnitFrame ДЕБАГ]: Вёрстка ProgressBar.uxml успешно вшита в слоты ХП и Ресурса.");
            }
            else
            {
                Debug.LogError("🚨 [UnitFrame ДЕБАГ]: _progressBarUxml равен NULL в инспекторе!");
            }

            // Инициализируем C# логику полосок
            _unitHealthBar = new HealthBar(healthSlot ?? _frameRoot);
            _unitResourceBar = new ResourceBar(resourceSlot ?? _frameRoot);
            Debug.Log("[UnitFrame ДЕБАГ]: Классы полосок HealthBar and ResourceBar успешно проинициализированы.");

            // =========================================================================
            // 🦾 ЧИСТЫЙ ААА-ИНИЦИАЛИЗАТОР АУР (КОПЕЙКА В КОПЕЙКУ КАК ХП-БАР!)
            // =========================================================================
            if (aurasSlot != null)
            {
                // Намертво рождаем класс, скармливая ему найденную вёрстку!
                _unitAuraFrame = new AuraFrame(aurasSlot, 8);
                Debug.Log("[UnitFrame]: AuraFrame успешно рождён и привязан к ноде 'aura-frame-root'.");
            }
            else
            {
                Debug.LogError("🚨 [UnitFrame]: В UXML фрейма не найдена нода 'aura-frame-root'!");
            }
        }

        // =========================================================================
        // 🎯 РЕАЛИЗАЦИЯ ИНТЕРФЕЙСОВ-РЕСИВЕРОВ ТВОЕГО КОНВЕЙЕРА (UIPullSystem)
        // =========================================================================
        
        /// <summary>
        /// Вызывается UIPullSystem, когда здоровье персонажа мутирует в ECS
        /// </summary>
        public void UpdateFromComponent(ref HealthComponent component)
        {
            // Перенаправляем сырые данные в твой рабочий метод апдейта
            UpdateHealth(component.Current, component.Max);
        }

        /// <summary>
        /// Вызывается UIPullSystem, когда ресурс (мана/энергия) персонажа мутирует от прыжка
        /// </summary>
        public void UpdateFromComponent(ref ResourceComponent component)
        {
            // Перенаправляем тип ресурса и сырые данные в твой рабочий метод апдейта
            UpdateResource(component.Type, component.Current, component.Max);
        }

        /// <summary>
        /// Вызывается UIPullSystem (СЛАЙС Д), когда данные паспорта мутируют или инициализируются в ECS
        /// </summary>
        public void UpdateFromComponent(ref UnitComponent component)
        {
            // Берем сырой NameKey из компонента, конвертируем в строку
            string rawNameKey = component.NameKey.ToString();

            // Извлекаем перевод из синглтона локализации и переводим в верхний регистр
            string localizedName = LocalizationManager.Get(rawNameKey).ToUpper();

            // Перенаправляем подготовленные данные в наш публичный метод апдейта
            UpdateIdentity(localizedName, component.Level);
        }

        // =========================================================================
        // ТВОИ ВНУТРЕННИЕ МЕТОДЫ ОБНОВЛЕНИЯ ВИЗУАЛА
        // =========================================================================

        public void UpdateHealth(float current, float max)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Получен апдейт ХП! Текущее: {current} / Макс: {max}");
            _unitHealthBar?.UpdateHealth(max > 0 ? current / max : 0f);
        }

        public void UpdateResource(ProjectTowerRpg.ECS.Components.ResourceType type, float current, float max)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Получен апдейт Ресурса! Тип: {type} | Текущее: {current} / Макс: {max}");
            _unitResourceBar?.UpdateResource(type, max > 0 ? current / max : 0f);
        }

        public void UpdateIdentity(string localizedName, int level)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Обновлен паспорт! Имя/Текст: {localizedName}, Уровень: {level}");
            
            if (_unitNameLabel != null) 
            {
                _unitNameLabel.text = localizedName;
            }

            if (_unitLevelLabel != null) 
            {
                _unitLevelLabel.text = $"Ур. {level}";
            }
        }

        public void SetVisible(bool visible)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Переключение видимости фрейма => {visible}");
            if (_frameRoot != null) _frameRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Кристально слепая привязка фрейма к любой сущности (Копейка в копейку как у Куклы!)
        /// </summary>
        public void BindToEntity(Entity entity)
        {
            if (_boundEntity != Entity.Null) UIRegistry.Unregister(_boundEntity, this);
            
            _boundEntity = entity;
            
            if (_boundEntity != Entity.Null) UIRegistry.Register(_boundEntity, this);

            // 🎯 СИ-ШЛЮЗ: Достаем изолированную сущность аур через BuffersLinkComponent!
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                var em = world.EntityManager;
                if (em.HasComponent<BuffersLinkComponent>(entity))
                {
                    var links = em.GetComponentData<BuffersLinkComponent>(entity);
                    
                    // Сажаем сетку аур на её СОБСТВЕННУЮ сущность-контейнер, копейка в копейку как инвентарь!
                    _unitAuraFrame?.BindToEntity(links.AuraFrame);
                    
                    Debug.Log($"🔮 [UnitFrame]: Найдена связь! Сетка аур юнита {entity.Index} успешно привязана к контейнеру {links.AuraFrame.Index}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ [UnitFrame]: Сущность {entity.Index} не имеет BuffersLinkComponent. Аура-фрейм ослеп.");
                }
            }

            // Твой старый WoW-канон сброса геометрии Unmount/Mount...
            if (_frameRoot != null && _frameRoot.parent != null)
            {
                var parentSlot = _frameRoot.parent;
                parentSlot.Remove(_frameRoot);
                parentSlot.Add(_frameRoot);
            }
        }
   }
}


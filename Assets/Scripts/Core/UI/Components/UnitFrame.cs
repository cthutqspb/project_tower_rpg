using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components; // Твой namespace с компонентами

namespace ProjectTowerRpg.Core.UI.Components
{
    [Serializable]
    // 🌟 ИСПРАВЛЕНО: Теперь класс официально подписывает контракт ресивера компонентов ECS!
    public class UnitFrame : IEcsUiComponentReceiver<HealthComponent>, IEcsUiComponentReceiver<ResourceComponent>, IEcsUiTargetReceiver
    {
        [Header("Список компонентов фрейма")]
        [SerializeField] private VisualTreeAsset _frameUxml;       // Сюда UnitFrame.uxml
        [SerializeField] private VisualTreeAsset _progressBarUxml; // Сюда ProgressBar.uxml
        public bool IsTargetFrame = false;

        private VisualElement _frameRoot;
        private Label _unitNameLabel;
        private Label _unitLevelLabel;
        
        private HealthBar _unitHealthBar;
        private ResourceBar _unitResourceBar;

        public UnitFrame() { }
        
        private Entity _boundEntity = Entity.Null;
       
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

            // Находим внутренние ноды текста и полосок
            var hpRoot = _frameRoot.Q<VisualElement>("health-bar-root");
            var resRoot = _frameRoot.Q<VisualElement>("resource-bar-root");
            _unitNameLabel = _frameRoot.Q<Label>("unit-name");
            _unitLevelLabel = _frameRoot.Q<Label>("unit-level");

            Debug.Log($"[UnitFrame ДЕБАГ]: Поиск внутренних нод: hpRoot={(hpRoot != null)}, resRoot={(resRoot != null)}, name={(_unitNameLabel != null)}, level={(_unitLevelLabel != null)}");

            // Вживляем внутренние полоски из списка компонентов фрейма
            if (_progressBarUxml != null)
            {
                hpRoot?.Add(_progressBarUxml.CloneTree());
                resRoot?.Add(_progressBarUxml.CloneTree());
                Debug.Log("[UnitFrame ДЕБАГ]: Вёрстка ProgressBar.uxml успешно вшита в слоты ХП и Ресурса.");
            }
            else
            {
                Debug.LogError("🚨 [UnitFrame ДЕБАГ]: _progressBarUxml равен NULL в инспекторе!");
            }

            // Инициализируем C# логику полосок
            _unitHealthBar = new HealthBar(hpRoot ?? _frameRoot);
            _unitResourceBar = new ResourceBar(resRoot ?? _frameRoot);
            Debug.Log("[UnitFrame ДЕБАГ]: Классы полосок HealthBar and ResourceBar успешно проинициализированы.");
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

        public void UpdateIdentity(string nameKey, int level)
        {
            Debug.Log($"[UnitFrame ДЕБАГ]: Обновлен паспорт! Имя/Ключ: {nameKey}, Уровень: {level}");
            if (_unitNameLabel != null) _unitNameLabel.text = nameKey;
            if (_unitLevelLabel != null) _unitLevelLabel.text = $"Ур. {level}";
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
            Debug.Log($"[UnitFrame ДЕБАГ]: Вызван метод BindToEntity для сущности с ECS-индексом: {entity.Index}");

            if (_boundEntity != Entity.Null)
            {
                Debug.Log($"[UnitFrame ДЕБАГ]: Выписываю старую сущность {_boundEntity.Index} из UIRegistry.");
                UIRegistry.Unregister(_boundEntity, this);
            }

            _boundEntity = entity;

            if (_boundEntity != Entity.Null)
            {
                UIRegistry.Register(_boundEntity, this);
                Debug.Log($"[UnitFrame ДЕБАГ]: Сущность {_boundEntity.Index} УСПЕШНО зарегистрирована в UIRegistry для этого фрейма!");
            }
        }

        public void UpdateTargetInfo(ref HealthComponent health, ref ResourceComponent resource)
        {
             // 🔒 ГВАРД-ПРЕДОХРАНИТЕЛЬ: Мой личный фрейм игрока игнорирует этот метод!
             if (!IsTargetFrame) return;

             SetVisible(true);
             UpdateHealth(health.Current, health.Max);

             if (resource.Type != ProjectTowerRpg.ECS.Components.ResourceType.None)
             {
                 UpdateResource(resource.Type, resource.Current, resource.Max);
             }

             UpdateIdentity("ЦЕЛЬ", 5); 
        }

        public void ClearTarget()
        {
            // 🔒 ГВАРД-ПРЕДОХРАНИТЕЛЬ: Мой личный фрейм игрока никогда не выключится от клика по земле!
            if (!IsTargetFrame) return;

            SetVisible(false);
        }
    }
}


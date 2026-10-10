using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Localization;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI.Components
{
    // 📊 ООП-КОНТРОЛЛЕР ХАРАКТЕРИСТИК (Чистокровный, автономный VisualElement)
    // Реализует два чистых интерфейса, чтобы зряче ловить и атрибуты, и боевые статы из ECS!
    public class UnitStats : VisualElement,
                             IEcsUiComponentReceiver<UnitCurrentAttributesComponent>, 
                             IEcsUiComponentReceiver<UnitCombatStatsComponent>
    {
        private Entity _unitEntity;
        
        // Ссылки на контейнеры групп внутри нашего UnitStatsComponent.uxml
        private readonly VisualElement _attributesContainer;
        private readonly VisualElement _combatStatsContainer;

        // Кэш для поиска элементов боевых стат, чтобы не делать тяжелый .Q() каждую миллисекунду
        private readonly Label _critLabel;
        private readonly Label _hitLabel;
        private readonly Label _attackSpeedLabel;
        private readonly Label _dodgeLabel;
        private readonly Label _parryLabel;
        private readonly Label _armorLabel;
        private readonly Label _magicResistLabel;

        // 🦾 ММО-КОНСТРУКТОР: Принимает строго VisualTreeAsset и клонирует дерево внутрь себя (this)
        public UnitStats(VisualTreeAsset uxml)
        {
            this.AddToClassList("unit-stats");

            if (uxml != null)
            {
                // Клонируем XML-структуру шаблона прямо внутрь этого VisualElement
                uxml.CloneTree(this);
                Debug.Log("[UnitStats] UXML шаблона характеристик успешно загружен");
            }
            else
            {
                Debug.LogError("[UnitStats] Ошибка: VisualTreeAsset шаблона равен null!");
                return;
            }

            // 🦾 АВТОНОМИЯ НА 100%: Ищем элементы строго внутри своего собственного склонированного дерева (this)!
            _attributesContainer = this.Q<VisualElement>("attributes");
            _combatStatsContainer = this.Q<VisualElement>("combat-stats");

            // Кэшируем лейблы вторичных боевых параметров по их зрячим CSS-именам из шаблона
            _critLabel = this.Q<Label>("crit-chance");
            _hitLabel = this.Q<Label>("hit-chance");
            _attackSpeedLabel = this.Q<Label>("attack-speed");
            _dodgeLabel = this.Q<Label>("dodge-chance");
            _parryLabel = this.Q<Label>("parry-chance");
            _armorLabel = this.Q<Label>("armor");
            _magicResistLabel = this.Q<Label>("magic-resist");
        }

        // =========================================================================
        // 🧬 ИНТЕРФЕЙС А: РЕАКТИВНОЕ ОБНОВЛЕНИЕ ПЕРВИЧНЫХ АТРИБУТОВ (Сила, Ловкость...)
        // =========================================================================
        public void UpdateFromComponent(ref UnitCurrentAttributesComponent component)
        {
            if (_attributesContainer == null) return;

            // Очищаем контейнер, чтобы динамически пересобрать список с учетом локализации
            _attributesContainer.Clear();

            // Массив-карта: связываем unmanaged-значения из ОЗУ со строковыми ключами твоего словаря!
            var attributesMap = new (string key, int value)[]
            {
                ("attribute_strength", component.strength),
                ("attribute_agility", component.agility),
                ("attribute_intellect", component.intellect),
                ("attribute_wisdom", component.wisdom),
                ("attribute_stamina", component.stamina)
            };

            // Динамически генерируем элементы списка в коде
            foreach (var attr in attributesMap)
            {
                var label = new Label();
                label.AddToClassList("unit-stats__label"); // Наш USS-класс шрифтов
                
                // Берем чистый перевод "Сила", "Ловкость" из твоего словаря и подставляем цифру
                string localizedName = LocalizationManager.Get(attr.key);
                label.text = $"{localizedName}: {attr.value}";

                _attributesContainer.Add(label);
            }
        }

        // =========================================================================
        // ⚔️ ИНТЕРФЕЙС Б: РЕАКТИВНОЕ ОБНОВЛЕНИЕ ВТОРИЧНЫХ БОЕВЫХ ПАРАМЕТРОВ
        // =========================================================================
        public void UpdateFromComponent(ref UnitCombatStatsComponent component)
        {
            // Берем переводы заголовков стат из локализации и сочно выводим живые unmanaged флоаты
            if (_critLabel != null) 
                _critLabel.text = $"{LocalizationManager.Get("stat_critical_chance")}: {component.CritChance:F1}%";
                
            if (_hitLabel != null) 
                _hitLabel.text = $"{LocalizationManager.Get("stat_hit_chance")}: {component.HitChance:F1}%";
                
            if (_attackSpeedLabel != null) 
                _attackSpeedLabel.text = $"{LocalizationManager.Get("stat_attack_speed")}: {component.AttackSpeed:F2}с";
                
            if (_dodgeLabel != null) 
                _dodgeLabel.text = $"{LocalizationManager.Get("stat_dodge_chance")}: {component.DodgeChance:F1}%";
                
            if (_parryLabel != null) 
                _parryLabel.text = $"{LocalizationManager.Get("stat_parry_chance")}: {component.ParryChance:F1}%";
                
            if (_armorLabel != null) 
                _armorLabel.text = $"{LocalizationManager.Get("stat_armor")}: {System.Math.Max(0, (int)component.Armor)}";
                
            if (_magicResistLabel != null) 
                _magicResistLabel.text = $"{LocalizationManager.Get("stat_magic_resist")}: {System.Math.Max(0, (int)component.MagicResist)}";
        }

        // 🦾 УЛЬТИМАТИВНЫЙ ММО-ДИНАМИЧЕСКИЙ BIND (BG3-Канон для Компаньонов):
        // Самостоятельно управляет регистрацией в UIRegistry при переключении персонажей!
        public void BindToEntity(Entity entity)
        {
            // 1. Если мы УЖЕ были привязаны к кому-то (например, к Игроку), 
            // мы принудительно выписываем этот UI-элемент из списков обновлений старого чара!
            if (_unitEntity != Entity.Null) 
            {
                UIRegistry.Unregister(_unitEntity, this);
            }
            
            // Запоминаем нашу новую цель (Компаньона или Игрока)
            _unitEntity = entity;
            
            // 2. Если новая сущность легитимна — шёлково регистрируем себя на её каналы обновлений UIPullSystem!
            if (_unitEntity != Entity.Null) 
            {
                UIRegistry.Register(_unitEntity, this);
            }

            // 3. Мгновенный Snapshot-вызов: Выдергиваем стартовые статы новой сущности из ОЗУ за 0 наносекунд,
            // чтобы экран обновился ТУТ ЖЕ в момент клика по портрету, не дожидаясь мутаций шмота!
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            
            if (em.Exists(_unitEntity))
            {
                // Выдергиваем и рендерим текущие атрибуты компаньона/игрока
                if (em.HasComponent<UnitCurrentAttributesComponent>(_unitEntity))
                {
                    var currentAttributes = em.GetComponentData<UnitCurrentAttributesComponent>(_unitEntity);
                    UpdateFromComponent(ref currentAttributes);
                }

                // Выдергиваем и рендерим вторичные боевые статы компаньона/игрока
                if (em.HasComponent<UnitCombatStatsComponent>(_unitEntity))
                {
                    var combatStats = em.GetComponentData<UnitCombatStatsComponent>(_unitEntity);
                    UpdateFromComponent(ref combatStats);
                }
            }
            else
            {
                // Если пришла пустая Entity — наглухо чистим списки лейблов, убирая старый мусор с экрана
                if (_attributesContainer != null) _attributesContainer.Clear();
                // Тут же можно занулить лейблы комбат статов, если они активны
            }
        }
     
    }
}


using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement,
                              IEcsUiBufferReceiver<ItemSlot>,
                              IEcsUiBufferReceiver<ActionBarSlot>,
                              IEntityContainer
    {
        private int _columns;
        private int _rows;
        private int _startIndex;
        private List<SlotElement> _slots = new();
        private Entity _boundEntity;

        public Entity BoundEntity => _boundEntity;

        public StaticGrid(int columns, int rows, int startIndex = 0)
        {
            _columns = columns;
            _rows = rows;
            _startIndex = startIndex;
            
            this.AddToClassList("static-grid-container");
            this.pickingMode = PickingMode.Ignore;
            
            this.style.width = columns * 48;
            this.style.flexDirection = FlexDirection.Row;
            this.style.flexWrap = Wrap.Wrap;
            this.style.flexShrink = 0;
            this.style.flexGrow = 0;

            // 🦾 ЧИСТЫЙ КОНСТРУКТОР: Вызываем выделенный Си-метод создания
            for (int i = 0; i < columns * rows; i++)
            {
                int bufferSlotIndex = _startIndex + i;
                var slot = CreateSlotElement(bufferSlotIndex, i);
                
                _slots.Add(slot);
                Add(slot);
            }
        }

        /// <summary>
        /// Выделенный изолированный фабричный метод сборки SlotElement
        /// </summary>
        private SlotElement CreateSlotElement(int bufferSlotIndex, int visualIndex)
        {
            var slot = new SlotElement
            {
                SlotIndex = bufferSlotIndex,
                ContainerEntity = _boundEntity, // Подхватит сущность, если она уже есть
                name = $"slot-{visualIndex}",
                style =
                {
                    width = 42,
                    height = 42,
                    marginTop = 2,
                    marginRight = 2,
                    marginBottom = 2,
                    marginLeft = 2,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f)
                }
            };

            // Навешиваем обязательный манипулятор для работы Drag-and-Drop
            var dragManipulator = new DragManipulator(slot, DragMode.Slot);
            slot.AddManipulator(dragManipulator);

            return slot;
        }

        /// <summary>
        /// WoW-канон: Адаптирует сетку под размеры сундука. 
        /// Если слотов в ОЗУ не хватает — динамически доращивает их через выделенный метод. 
        /// Лишние слоты скрывает через display: None, полностью исключая фризы!
        /// </summary>
        public void ResizeAndExpand(int targetColumns, int targetRows)
        {
            _columns = targetColumns;
            _rows = targetRows;

            // Корректируем ширину контейнера под текущую геометрию сундука
            this.style.width = targetColumns * 48;

            int requiredSlotsCount = targetColumns * targetRows;

            // 🦾 ЧИСТОЕ РАСШИРЕНИЕ ПУЛА: Если прилетел босс с огромным мешком лута
            if (_slots.Count < requiredSlotsCount)
            {
                int currentCount = _slots.Count;
                int slotsToCreate = requiredSlotsCount - currentCount;
                Debug.Log($"🚀 [StaticGrid]: Расширяем пул слотов ({currentCount} -> {requiredSlotsCount}). Досоздаем {slotsToCreate} ячеек...");

                for (int i = 0; i < slotsToCreate; i++)
                {
                    int slotVisualIndex = currentCount + i;
                    int bufferSlotIndex = _startIndex + slotVisualIndex;

                    // Вызываем тот же самый чистый метод без дублирования USS-стилей!
                    var slot = CreateSlotElement(bufferSlotIndex, slotVisualIndex);

                    _slots.Add(slot);
                    Add(slot);
                }
            }

            // 🦾 WoW-КАНОН УПРАВЛЕНИЯ ВИДИМОСТЬЮ:
            // Включаем нужные ячейки, а лишние отправляем спать без уничтожения меша
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < requiredSlotsCount)
                {
                    _slots[i].style.display = DisplayStyle.Flex;
                    _slots[i].ContainerEntity = _boundEntity; // Актуализируем паспорт сущности
                }
                else
                {
                    _slots[i].style.display = DisplayStyle.None;
                }
            }
        }

        public void BindToEntity(Entity targetEntity)
        {
            Debug.Log($"[StaticGrid] BindToEntity для сущности: {targetEntity}");
            
            _boundEntity = targetEntity;
            
            foreach (var slot in _slots)
            {
                slot.ContainerEntity = targetEntity;
            }

            UIRegistry.Register(targetEntity, this);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            if (em.HasBuffer<ItemSlot>(targetEntity))
            {
                var slots = em.GetBuffer<ItemSlot>(targetEntity);
                UpdateFromBuffer(slots);
            }
            else
            {
                Debug.LogWarning($"[StaticGrid] Сущность {targetEntity} не имеет универсального буфера ItemSlot");
            }
        }

        // =========================================================================
        // ⚔️ ПОТОК ИНВЕНТАРЯ (Вызывается автоматически для DynamicBuffer<ItemSlot>)
        // =========================================================================
        public void UpdateFromBuffer(
            DynamicBuffer<ItemSlot> slots,
            bool isOnlyValidation = true,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer<ActiveCooldownElement> cooldowns = default
        )
        {
            Debug.Log($"[StaticGrid] UpdateFromBuffer: {slots.Length} предметов инвентаря.");
            
            foreach (var slot in _slots)
            {
                slot.ClearVisual();
            }

            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var slotData = slots[i];
                var itemId = slotData.DataId.ToString();
                var config = !string.IsNullOrEmpty(itemId) ? ItemsDatabase.GetItem(itemId) : null;
                
                _slots[i].SetData(itemId, i, slotData.Amount);
            }
        }

        // =========================================================================
        // 🔮 РЕЛЬСЫ ЭКШЕН-БАРА (Слепо и реактивно рендерит ВСЕ хоткеи 0..23)
        // =========================================================================
        public void UpdateFromBuffer(
            DynamicBuffer<ActionBarSlot> slots,
            bool isOnlyValidation = false,
            float gcdRemaining = 0f,
            float gcdDuration = 0f,
            DynamicBuffer<ActiveCooldownElement> cooldowns = default
        )
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            // ✅ ЕСЛИ ТОЛЬКО ВАЛИДАЦИЯ — НЕ ЧИСТИМ ВИЗУАЛ
            if (!isOnlyValidation)
            {
                foreach (var slot in _slots) slot.ClearVisual();
            }

            DynamicBuffer<ItemSlot> inventorySlots = default;
            bool hasValidInventory = false;

            // 🦾 СИ-ЗАЩИТА ОЗУ: Лезем в рюкзак СТРОГО в тяжелом потоке структуры!
            // Дополнительно страхуем чанки через легальный Си-флаг isReadOnly: true.
            if (!isOnlyValidation && _boundEntity != Entity.Null && em.Exists(_boundEntity) && em.HasComponent<BuffersLinkComponent>(_boundEntity))
            {
                Entity inventoryEntity = em.GetComponentData<BuffersLinkComponent>(_boundEntity).Inventory;
                if (inventoryEntity != Entity.Null && em.Exists(inventoryEntity) && em.HasBuffer<ItemSlot>(inventoryEntity))
                {
                    inventorySlots = em.GetBuffer<ItemSlot>(inventoryEntity, isReadOnly: true);
                    hasValidInventory = true;
                }
            }

            for (int i = 0; i < _slots.Count && (i + _startIndex) < slots.Length; i++)
            {
                int absoluteIndex = i + _startIndex;
                var slotData = slots[absoluteIndex];
                var abilityId = slotData.AbilityId.ToString();
                
                // 🦾 СИ-ГВАРД НА ПУСТОЙ СЛОТ: Очищаем зависший кулдаун, если скилл утащили драгом
                if (string.IsNullOrEmpty(abilityId)) 
                {
                    // Мгновенно тушим «часики» кулдауна на этой ячейке
                    _slots[i].SetCooldown(0f, 0f, false);
                    
                    // Если это ПОЛНЫЙ рендеринг (а не просто валидация), принудительно затираем старый визуал
                    if (!isOnlyValidation)
                    {
                        _slots[i].ClearVisual();
                    }
                    continue; // Теперь безопасно скипаем пустой слот
                }

                // ✅ ВАЛИДАЦИЯ (всегда)
                var validationResult = AbilityValidator.CheckCastPossibility(abilityId, _boundEntity, em);

                // 🎯 ВЫЧИСЛЯЕМ ЛИЧНЫЙ КУЛДАУН СПОСОБНОСТЕЙ
                float abilityCooldownRemaining = 0f;
                float abilityCooldownDuration = 0f;

                // 🦾 СИ-ОПТИМИЗАЦИЯ: Убрали ненадежную проверку типа слота при драге.
                // Проверяем КД по базе для ЛЮБОГО непустого abilityId.
                if (cooldowns.IsCreated)
                {
                    var abilityCfg = AbilitiesDatabase.GetAbility(abilityId);
                    if (abilityCfg != null && abilityCfg.parameters != null)
                    {
                        // Извлекаем CooldownGroup из базы (или юзаем сам abilityId, если группа не задана)
                        Unity.Collections.FixedString32Bytes cooldownGroup = abilityCfg.parameters.cooldown_group ?? abilityId;

                        for (int c = 0; c < cooldowns.Length; c++)
                        {
                            if (cooldowns[c].CooldownGroup == cooldownGroup)
                            {
                                abilityCooldownRemaining = cooldowns[c].Remaining;
                                abilityCooldownDuration = cooldowns[c].Duration;
                                break;
                            }
                        }
                    }
                }

                // ⚔️ WoW-ПРИОРИТЕТ: Выбираем то, что остывает ДОЛЬШЕ (ГКД или собственный КД заклинания)
                float cooldownRemaining = gcdRemaining > abilityCooldownRemaining ? gcdRemaining : abilityCooldownRemaining;
                float cooldownDuration = cooldownRemaining == gcdRemaining ? gcdDuration : abilityCooldownDuration;
                bool isGcdActive = cooldownRemaining == gcdRemaining;

                // ✅ ЕСЛИ ТОЛЬКО ВАЛИДАЦИЯ — ОБНОВЛЯЕМ ТОЛЬКО ЦВЕТ/ПРОЗРАЧНОСТЬ И КУЛДАУН
                // Процессор наглухо скипает весь тяжелый код ниже, инвентарь девственно чист!
                if (isOnlyValidation)
                {
                    _slots[i].SetValidation(validationResult);
                    // Передаем вычисленный кулдаун и флаг типа КД в твой обновленный графический метод слота
                    _slots[i].SetCooldown(cooldownRemaining, cooldownDuration, isGcdActive);
                    continue;
                }             

                // ✅ ПОЛНЫЙ РЕНДЕРИНГ (иконка, количество, бинд)
                int displayAmount = 1;

                if (slotData.SlotType == "item")
                {
                    displayAmount = 0;
                    if (hasValidInventory)
                    {
                        for (int idx = 0; idx < inventorySlots.Length; idx++)
                        {
                            if (inventorySlots[idx].DataId == slotData.AbilityId)
                            {
                                displayAmount += inventorySlots[idx].Amount;
                            }
                        }
                    }
                }
                
                string bindingText = slotData.KeyBinding.ToString();
                _slots[i].SetData(abilityId, absoluteIndex, displayAmount, true, bindingText, validationResult);
                
                // 🦾 ПРАВИЛЬНЫЙ НАКАТ КД: Мы ВСЕГДА накатываем кулдаун в конце SetData.
                _slots[i].SetCooldown(cooldownRemaining, cooldownDuration, isGcdActive);
            }
        }
    }
}

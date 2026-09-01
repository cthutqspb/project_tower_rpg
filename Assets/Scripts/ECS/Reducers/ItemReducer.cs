using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Reducers
{
    public static class ItemReducer
    {        

        // 🦾 СТЕРИЛЬНЫЙ unmanaged-ОЧИСТИТЕЛЬ СЛОТОВ В БЭКЕНДЕ (Иммутабельный канон):
        // Работает напрямую с ECS-буфером чанка. Изменяет ТОЛЬКО живой контент!
        public static void ClearSlot(DynamicBuffer<ItemSlot> slots, int index)
        {
            if (index < 0 || index >= slots.Length) return;

            // Выдергиваем мутабельную Си-ячейку памяти прямо из ОЗУ чанка ECS
            var targetSlot = slots[index];
            
            // НАГЛУХО СТИРАЕМ ТОЛЬКО ВЛОЖЕННЫЙ КОНТЕНТ!
            targetSlot.DataId = "";
            targetSlot.DataType = "";
            targetSlot.Amount = 0;
            targetSlot.ItemEntity = Unity.Entities.Entity.Null; // Ссылка на сущность в мире стёрта!
            
            // 🚨 ПАСПОРТ СЛОТА (SlotIndex, ContainerType, EquipSlot) ОСТАЕТСЯ АБСОЛЮТНО НЕПРИКОСНОВЕННЫМ!
            // Кукла железно сохранит свой "MAIN_HAND" / "CHEST", инвентарь сохранит свой индекс!
            slots[index] = targetSlot; 
            
            Debug.Log($"🎒 [ItemReducer.ClearSlot]: Точечно очищен контент чанка в слоте #{index}. Паспорт ячейки и анатомия '{targetSlot.EquipSlot}' неприкосновенны.");
        }

        // ================================================================
        // УНИВЕРСАЛЬНЫЙ ТРАНСФЕР (ПОЛИМОРФНЫЙ)
        // ================================================================

        public static void Transfer(ISlotContainer source, int sourceSlot, ISlotContainer target, int targetSlot)
        {
            if (!source.HasContent(sourceSlot))
            {
                Debug.LogWarning($"[ItemActions] В источнике нет контента в слоте #{sourceSlot}");
                return;
            }

            var content = source.GetContent(sourceSlot);

            // 1. Первая стандартная проверка: может ли цель принять наш предмет?
            if (!target.CanPlaceContent(targetSlot, content))
            {
                Debug.LogWarning($"[ItemActions] Нельзя поместить контент в слот #{targetSlot}");
                return;
            }

            // 🟢 КЕЙС А: Целевой слот пустой → обычное перемещение (Твой родной рабочий код)
            if (!target.HasContent(targetSlot))
            {
                target.SetContent(targetSlot, content);
                source.ClearSlot(sourceSlot);
                Debug.Log($"[ItemActions] Перенос из слота #{sourceSlot} в слот #{targetSlot}");
                return;
            }

            // 🔴 КЕЙС Б: Целевой слот ЗАНЯТ → Готовим СВОП (Рокировку)
            var targetContent = target.GetContent(targetSlot);

            // =========================================================================
            // 🦾 ВЕЛИКИЙ ММО-ГВАРД ДВУСТОРОННЕЙ АНАТОМИИ (Защита от вакханалии):
            // Перед тем как менять вещи местами, мы ОБЯЗАНЫ спросить у исходного контейнера:
            // "Эй, а ты сможешь принять в свой sourceSlot тот предмет, который вернется тебе при обмене?"
            // =========================================================================
            if (!source.CanPlaceContent(sourceSlot, targetContent))
            {
                Debug.LogWarning($"[ItemActions] Блокировка Свопа: Исходный слот #{sourceSlot} не может принять возвращаемый предмет '{((ItemSlot)targetContent).DataId}'!");
                return; // Намертво гасим транзакцию, спасая куклу от заклинивания!
            }

            // Если обе стороны согласны на обмен — шёлково свопаем шмотки в ОЗУ
            target.SetContent(targetSlot, content);
            source.SetContent(sourceSlot, targetContent);
            Debug.Log($"[ItemActions] Своп слотов #{sourceSlot} ↔ #{targetSlot}");
        }

        // ================================================================
        // DROP
        // ================================================================

        public static void Drop(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity containerEntity,
            int index,
            float3 position)
        {
            if (!slotDataLookup.HasBuffer(containerEntity))
            {
                Debug.LogWarning("[ItemActions] У сущности нет буфера ItemSlot");
                return;
            }

            var slots = slotDataLookup[containerEntity];

            if (index < 0 || index >= slots.Length)
            {
                Debug.LogWarning($"[ItemActions] Неверный index слота для дропа: {index}");
                return;
            }

            var item = slots[index];
            if (item.IsEmpty)
            {
                Debug.LogWarning($"[ItemActions] В слоте {index} нет данных для дропа");
                return;
            }

            Entity itemEntity = item.ItemEntity;

            // =========================================================================
            // 🦾 ПУЛЕНЕПРОБИВАЕМЫЙ unmanaged-ГВАРД ЧЕРЕЗ ECB (Спасение куклы без краша):
            // Если на кукле или в сумке у шмотки лежал Entity.Null, мы рождаем Entity
            // СТРОГО через отложенный ecb! Никаких EntityManager.CreateEntity живьем!
            // =========================================================================
            if (itemEntity == Entity.Null)
            {
                // Рождаем виртуальную отложенную Entity в буфере команд!
                itemEntity = ecb.CreateEntity();
                
                // Накатываем данные через ecb! Это на 100% безопасно внутри любых циклов!
                ecb.AddComponent(itemEntity, new ItemComponent
                {
                    Uid = item.DataId.GetHashCode() + index,
                    ItemId = item.DataId,
                    Amount = item.Amount,
                    IsLooted = false
                });
                
                Debug.LogWarning($"⚠️ [ItemActions.Drop]: На кукле/в сумке лежал Entity.Null! Через ECB создана аварийная Entity для {item.DataId}.");
            }

            // Освобождаем ячейку контейнера (инвентаря или куклы)
            ClearSlot(slots, index);

            float3 dropPosition = position;
            dropPosition.y = ProjectTowerRpg.Core.PhysicsUtils.GetGroundHeight(dropPosition);

            // 🦾 Теперь всё пишется строго по рельсам ECB. Никаких Structural Changes в рантайме!
            ecb.AddComponent(itemEntity, Unity.Transforms.LocalTransform.FromPosition(dropPosition));
            ecb.RemoveComponent<StoredTag>(itemEntity); 

            Debug.Log($"[ItemActions.Drop] Предмет {item.DataId} успешно отправлен в ECB на дроп в позицию {dropPosition}.");
        }

        // ================================================================
        // LOOT
        // ================================================================

        public static void Loot(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity itemEntity,
            Entity inventoryEntity)
        {
            if (!slotDataLookup.HasBuffer(inventoryEntity)) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<ItemComponent>(itemEntity)) return;

            // 🦾 ПУЛЕНЕПРОБИВАЕМЫЙ ММО-ГВАРД: 
            // Если на предмете УЖЕ висит StoredTag — значит, его уже подняли в этом или прошлом кадре!
            // Защита от двойного клика сработала, наглухо выходим!
            if (em.HasComponent<StoredTag>(itemEntity)) return;

            var itemData = em.GetComponentData<ItemComponent>(itemEntity);
            var inventoryBuffer = slotDataLookup[inventoryEntity];

            int targetSlotIndex = -1;
            for (int i = 0; i < inventoryBuffer.Length; i++)
            {
                if (inventoryBuffer[i].IsEmpty) { targetSlotIndex = i; break; }
            }

            if (targetSlotIndex != -1)
            {
                // Записываем вечную Entity меча в карман рюкзака
                inventoryBuffer[targetSlotIndex] = new ItemSlot
                {
                    SlotIndex = targetSlotIndex,
                    DataId = itemData.ItemId,
                    DataType = "item",
                    Amount = itemData.Amount,
                    ItemEntity = itemEntity, 
                    EquipSlot = EquipSlot.NONE,
                    ContainerType = ContainerType.INVENTORY
                };

                // =========================================================================
                // 🦾 ВЕЛИКАЯ ММО-УПАКОВКА:
                // Мы просто НАВЕШИВАЕМ пустой тег StoredTag через ECB!
                // Системы видимости и очистки кубов мгновенно увидят этот тег 
                // и сотрут 3D-модель со сцены на следующем же кадре!
                // =========================================================================
                ecb.AddComponent<StoredTag>(itemEntity);
                
                // Если на предмете висел технический тег графики — тоже гасим его
                if (em.HasComponent<VisualizedTag>(itemEntity))
                {
                    ecb.RemoveComponent<VisualizedTag>(itemEntity);
                }

                Debug.Log($"[ItemActions.Loot] 'Душа' предмета {itemData.ItemId} (Entity {itemEntity.Index}) успешно упакована в рюкзак.");
            }
        }

        // ================================================================
        // USE
        // ================================================================

        public static void Use(
            ref BufferLookup<ItemSlot> slotDataLookup,
            Entity containerEntity,
            int index,
            string itemId,
            Entity casterEntity) // ИСПРАВЛЕНО: Геймдеверский нейминг casterEntity вместо веб-мусора
        {
            var config = ItemsDatabase.GetItem(itemId);
            if (config == null)
            {
                Debug.LogWarning($"[ItemActions] Предмет {itemId} не найден в базе");
                return;
            }

            if (config.use_effects == null || config.use_effects.Count == 0)
            {
                Debug.LogWarning($"[ItemActions] У предмета {itemId} нет use_effects");
                return;
            }

            foreach (var effect in config.use_effects)
            {
                ApplyItemEffect(effect, casterEntity, config); // Передаем кастера
            }

            if (config.properties.stackable && config.properties.max_stack > 0)
            {
                if (!slotDataLookup.HasBuffer(containerEntity))
                {
                    Debug.LogWarning("[ItemActions] У контейнера нет буфера ItemSlot");
                    return;
                }

                var slots = slotDataLookup[containerEntity];

                if (index < 0 || index >= slots.Length) return;

                var item = slots[index];
                if (item.Amount > 1)
                {
                    slots[index] = new ItemSlot
                    {
                        SlotIndex = index,
                        ContainerType = item.ContainerType,
                        DataId = item.DataId,
                        DataType = item.DataType,
                        Amount = item.Amount - 1,
                        EquipSlot = item.EquipSlot
                    };
                }
                else
                {
                    ClearSlot(slots, index);
                }
            }

            Debug.Log($"[ItemActions] Использован предмет {itemId} кастером {casterEntity}");
        }

        // ================================================================
        // ПРИМЕНЕНИЕ ЭФФЕКТОВ
        // ================================================================

        private static void ApplyItemEffect(ItemUseEffect effect, Entity casterEntity, ItemConfig config) // ИСПРАВЛЕНО: casterEntity
        {
            if (effect.ability_id == "heal")
            {
                Debug.Log($"[ItemActions] Лечение {effect.value?.min}-{effect.value?.max} HP для {casterEntity}");
            }
            else if (effect.ability_id == "mana")
            {
                Debug.Log($"[ItemActions] Восполнение маны {effect.value?.min}-{effect.value?.max} для {casterEntity}");
            }
            else if (effect.ability_id == "buff")
            {
                Debug.Log($"[ItemActions] Наложение аур на {casterEntity}");
            }
            else if (effect.ability_id == "summon")
            {
                Debug.Log($"[ItemActions] Призыв питомца для {casterEntity}");
            }
            else
            {
                Debug.Log($"[ItemActions] Применение эффекта {effect.ability_id} к {casterEntity}");
            }
        }
    }
}


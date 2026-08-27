using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Actions
{
    public static class ItemActions
    {
        // ================================================================
        // ОЧИСТКА СЛОТОВ
        // ================================================================

        public static void ClearSlot(DynamicBuffer<ItemSlot> slots, int index)
        {
            slots[index] = new ItemSlot
            {
                SlotIndex = index,
                ContainerType = slots[index].ContainerType, // Сохраняем тип контейнера (например, Inventory)
                DataId = "",
                DataType = "",
                Amount = 0,
                EquipSlot = EquipSlot.NONE // Используем универсальный enum
            };
        }

        // ================================================================
        // УНИВЕРСАЛЬНЫЙ ТРАНСФЕР (ПОЛИМОРФНЫЙ)
        // ================================================================

        public static void Transfer(ISlotContainer source, int sourceSlot, ISlotContainer target, int targetSlot)
        {
            // Проверяем, есть ли контент в источнике
            if (!source.HasContent(sourceSlot))
            {
                Debug.LogWarning($"[ItemActions] В источнике нет контента в слоте #{sourceSlot}");
                return;
            }

            var content = source.GetContent(sourceSlot);

            // Проверка возможности размещения через полиморфный интерфейс целевого контейнера
            if (!target.CanPlaceContent(targetSlot, content))
            {
                Debug.LogWarning($"[ItemActions] Нельзя поместить контент в слот #{targetSlot}");
                return;
            }

            // Если целевой слот пустой → перемещаем
            if (!target.HasContent(targetSlot))
            {
                target.SetContent(targetSlot, content);
                source.ClearSlot(sourceSlot);
                Debug.Log($"[ItemActions] Перенос из слота #{sourceSlot} в слот #{targetSlot}");
                return;
            }

            // Если целевой слот занят → своп (меняем контент местами)
            var targetContent = target.GetContent(targetSlot);
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
            string itemId,
            int amount,
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

            ClearSlot(slots, index);

            Entity requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new DropItemRequest
            {
                ItemId = itemId,
                Amount = amount,
                Position = position,
                LootTableId = "", // если нужно
                RespawnTime = 0,  // если нужно
                IsLooted = false  // ← ДОБАВИТЬ!
            });

            Debug.Log($"[ItemActions] Запрос на спавн предмета {itemId} x{amount} отправлен в ItemSpawnSystem");
        }

        // ================================================================
        // LOOT
        // ================================================================

        public static void Loot(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity itemWorldEntity,
            Entity inventoryEntity)
        {
            if (!slotDataLookup.HasBuffer(inventoryEntity))
            {
                Debug.LogError($"[ItemActions.Loot] У сущности инвентаря {inventoryEntity.Index} отсутствует DynamicBuffer<ItemSlot>!");
                return;
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<ItemComponent>(itemWorldEntity))
            {
                Debug.LogWarning($"[ItemActions.Loot] Предмет {itemWorldEntity.Index} больше не существует или уже собран!");
                return;
            }

            var itemData = em.GetComponentData<ItemComponent>(itemWorldEntity);
            var inventoryBuffer = slotDataLookup[inventoryEntity];

            int targetSlotIndex = -1;
            for (int i = 0; i < inventoryBuffer.Length; i++)
            {
                if (inventoryBuffer[i].IsEmpty)
                {
                    targetSlotIndex = i;
                    break;
                }
            }

            if (targetSlotIndex != -1)
            {
                inventoryBuffer[targetSlotIndex] = new ItemSlot
                {
                    SlotIndex = targetSlotIndex,
                    DataId = itemData.ItemId,
                    DataType = "item",
                    Amount = itemData.Amount,
                    EquipSlot = EquipSlot.NONE,
                    ContainerType = ContainerType.INVENTORY
                };

                Debug.Log($"[ItemActions.Loot] Предмет {itemData.ItemId} перенесён в слот #{targetSlotIndex}");
                ecb.DestroyEntity(itemWorldEntity);
            }
            else
            {
                Debug.LogWarning("[ItemActions.Loot] Инвентарь полон!");
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


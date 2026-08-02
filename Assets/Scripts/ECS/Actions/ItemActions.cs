using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Actions
{
    public static class ItemActions
    {
        public static void Transfer(ref BufferLookup<SlotData> slotDataLookup,
                                    Entity sourceEntity, int sourceSlot,
                                    Entity targetEntity, int targetSlot,
                                    string itemId, int amount)
        {
            if (!slotDataLookup.HasBuffer(sourceEntity) || !slotDataLookup.HasBuffer(targetEntity))
            {
                Debug.LogWarning("[ItemActions] У источника или цели нет буфера SlotData");
                return;
            }

            var sourceSlots = slotDataLookup[sourceEntity];
            var targetSlots = slotDataLookup[targetEntity];

            if (sourceSlot < 0 || sourceSlot >= sourceSlots.Length ||
                targetSlot < 0 || targetSlot >= targetSlots.Length)
            {
                Debug.LogWarning($"[ItemActions] Неверный индекс слота");
                return;
            }

            var sourceItem = sourceSlots[sourceSlot];
            if (sourceItem.IsEmpty)
            {
                Debug.LogWarning($"[ItemActions] В слоте {sourceSlot} нет данных");
                return;
            }

            if (sourceItem.DataType.ToString() != "item")
            {
                Debug.LogWarning($"[ItemActions] Слот содержит не предмет: {sourceItem.DataType}");
                return;
            }

            var targetItem = targetSlots[targetSlot];
            if (!targetItem.IsEmpty)
            {
                // Стакание
                if (targetItem.DataId.ToString() == itemId && targetItem.DataType.ToString() == "item")
                {
                    targetSlots[targetSlot] = new SlotData
                    {
                        SlotIndex = targetSlot,
                        ContainerType = targetItem.ContainerType,
                        DataId = targetItem.DataId,
                        DataType = targetItem.DataType,
                        Amount = targetItem.Amount + amount,
                        EquipSlot = targetItem.EquipSlot
                    };

                    sourceSlots[sourceSlot] = new SlotData
                    {
                        SlotIndex = sourceSlot,
                        ContainerType = sourceItem.ContainerType,
                        DataId = "",
                        DataType = "",
                        Amount = 0,
                        EquipSlot = ""
                    };

                    Debug.Log($"[ItemActions] Стакание {itemId} x{amount} в слот {targetSlot}");
                    return;
                }

                Debug.Log($"[ItemActions] В слоте {targetSlot} уже есть предмет");
                return;
            }

            // Записываем в целевой слот, сохраняя ЕГО родной индекс и тип контейнера
            targetSlots[targetSlot] = new SlotData
            {
                SlotIndex = targetSlot,
                ContainerType = targetItem.ContainerType,
                DataId = sourceItem.DataId,
                DataType = sourceItem.DataType,
                Amount = sourceItem.Amount,
                EquipSlot = sourceItem.EquipSlot
            };

            // Записываем в исходный слот (очищаем его), сохраняя ЕГО родной индекс и тип контейнера
            sourceSlots[sourceSlot] = new SlotData
            {
                SlotIndex = sourceSlot,
                ContainerType = sourceItem.ContainerType,
                DataId = "",
                DataType = "",
                Amount = 0,
                EquipSlot = ""
            };

            Debug.Log($"[ItemActions] Перенос предмета {itemId} из слота {sourceSlot} в {targetSlot}");
        }

        public static void Drop(ref BufferLookup<SlotData> slotDataLookup, EntityCommandBuffer ecb, Entity containerEntity, int slot, string itemId, int amount, float3 position)
        {
            if (!slotDataLookup.HasBuffer(containerEntity))
            {
                Debug.LogWarning("[ItemActions] У сущности нет буфера SlotData");
                return;
            }

            var slots = slotDataLookup[containerEntity];

            if (slot < 0 || slot >= slots.Length)
            {
                Debug.LogWarning($"[ItemActions] Неверный индекс слота: {slot}");
                return;
            }

            var item = slots[slot];
            if (item.IsEmpty)
            {
                Debug.LogWarning($"[ItemActions] В слоте {slot} нет данных");
                return;
            }

            if (item.DataType.ToString() != "item")
            {
                Debug.LogWarning($"[ItemActions] Слот содержит не предмет: {item.DataType}");
                return;
            }

            // 1. КАНОН: Очищаем ячейку в ECS-буфере памяти инвентаря
            slots[slot] = new SlotData
            {
                SlotIndex = slot,
                ContainerType = item.ContainerType,
                DataId = "",
                DataType = "",
                Amount = 0,
                EquipSlot = ""
            };

            // 2. КАНОН: Вместо спавна куба создаем отложенный запрос на спавн через ECB
            Entity requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new DropItemRequest
            {
                ItemId = itemId,
                Amount = amount,
                Position = position
            });

            Debug.Log($"[ItemActions] Запрос на спавн предмета {itemId} x{amount} отправлен в ItemSpawnSystem");
        }

        public static void Use(ref BufferLookup<SlotData> slotDataLookup, Entity containerEntity, int slot, string itemId, Entity userEntity)
        {
            var config = ItemsDatabase.GetItem(itemId);
            if (config == null)
            {
                Debug.LogWarning($"[ItemActions] Предмет {itemId} не найден в базе");
                return;
            }

            // Проверяем use_effects (массив)
            if (config.use_effects == null || config.use_effects.Count == 0)
            {
                Debug.LogWarning($"[ItemActions] У предмета {itemId} нет use_effects");
                return;
            }

            // Применяем все эффекты
            foreach (var effect in config.use_effects)
            {
                ApplyItemEffect(effect, userEntity, config);
            }

            // Проверяем, расходный ли предмет (stackable == true, но не бесконечный)
            if (config.properties.stackable && config.properties.max_stack > 0)
            {
                if (!slotDataLookup.HasBuffer(containerEntity))
                {
                    Debug.LogWarning("[ItemActions] У контейнера нет буфера SlotData");
                    return;
                }

                var slots = slotDataLookup[containerEntity];
                if (slot < 0 || slot >= slots.Length) return;

                var item = slots[slot];
                if (item.Amount > 1)
                {
                    slots[slot] = new SlotData
                    {
                        SlotIndex = slot,
                        ContainerType = item.ContainerType,
                        DataId = item.DataId,
                        DataType = item.DataType,
                        Amount = item.Amount - 1,
                        EquipSlot = item.EquipSlot
                    };
                }
                else
                {
                    slots[slot] = new SlotData
                    {
                        SlotIndex = slot,
                        ContainerType = item.ContainerType,
                        DataId = "",
                        DataType = "",
                        Amount = 0,
                        EquipSlot = ""
                    };
                }
            }

            Debug.Log($"[ItemActions] Использован предмет {itemId} пользователем {userEntity}");
        }

        public static void Equip(Entity containerEntity, int slot, string itemId, Entity userEntity)
        {
            // Метод пока пустой, оставляем как в оригинале
            var config = ItemsDatabase.GetItem(itemId);
            if (config == null)
            {
                Debug.LogWarning($"[ItemActions] Предмет {itemId} не найден в базе");
                return;
            }

            if (string.IsNullOrEmpty(config.properties.equip_slot))
            {
                Debug.LogWarning($"[ItemActions] Предмет {itemId} нельзя экипировать");
                return;
            }

            Debug.Log($"[ItemActions] Экипировка предмета {itemId} на слот {config.properties.equip_slot}");
        }

        private static void ApplyItemEffect(ItemUseEffect effect, Entity userEntity, ItemConfig config)
        {
            if (effect.ability_id == "heal")
            {
                Debug.Log($"[ItemActions] Лечение {effect.value?.min}-{effect.value?.max} HP");
            }
            else if (effect.ability_id == "mana")
            {
                Debug.Log($"[ItemActions] Восполнение маны {effect.value?.min}-{effect.value?.max}");
            }
            else if (effect.ability_id == "buff")
            {
                Debug.Log($"[ItemActions] Наложение аур");
            }
            else if (effect.ability_id == "summon")
            {
                Debug.Log($"[ItemActions] Призыв питомца");
            }
            else
            {
                Debug.Log($"[ItemActions] Применение эффекта {effect.ability_id} к {userEntity}");
            }
        }
    }
}


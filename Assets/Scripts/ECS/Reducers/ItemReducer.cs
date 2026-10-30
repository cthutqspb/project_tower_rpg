using Unity.Transforms;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Reducers
{
    public static class ItemReducer
    {
        // 🎯 Счётчик для уникальных Uid новых душ (сплиты одного и того же стака)
        private static int _splitUidCounter = 10000;

        // ================================================================
        // ОЧИСТИТЕЛЬ СЛОТОВ
        // ================================================================
        public static void ClearSlot(DynamicBuffer<ItemSlot> slots, int index)
        {
            if (index < 0 || index >= slots.Length) return;

            var targetSlot = slots[index];
            targetSlot.DataId = "";
            targetSlot.ContentType = SlotContentType.Empty;
            targetSlot.Amount = 0;
            targetSlot.ItemEntity = Entity.Null;

            slots[index] = targetSlot;

            Debug.Log($"🎒 [ItemReducer.ClearSlot]: Очищен слот #{index}. Паспорт '{targetSlot.EquipSlot}' сохранён.");
        }

        // ================================================================
        // УНИВЕРСАЛЬНЫЙ ТРАНСФЕР
        // ================================================================
        public static void Transfer(
            EntityCommandBuffer ecb,
            ISlotContainer source,
            int sourceSlot,
            ISlotContainer target,
            int targetSlot,
            Entity presentationBufferEntity)
        {
            if (!source.HasContent(sourceSlot))
            {
                Debug.LogWarning($"[ItemActions] В источнике нет контента в слоте #{sourceSlot}");
                return;
            }

            var content = source.GetContent(sourceSlot);

            if (!target.CanPlaceContent(targetSlot, content))
            {
                Debug.LogWarning($"[ItemActions] Нельзя поместить контент в слот #{targetSlot}");
                return;
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // КЕЙС А: целевой слот пустой
            if (!target.HasContent(targetSlot))
            {
                target.SetContent(targetSlot, content);
                source.ClearSlot(sourceSlot);

                var playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>(em);
                if (playerEntity != Entity.Null && em.HasComponent<BuffersLinkComponent>(playerEntity))
                {
                    var links = em.GetComponentData<BuffersLinkComponent>(playerEntity);

                    if (source.Entity != links.Inventory && target.Entity == links.Inventory)
                    {
                        ItemSlot itemSlot = (ItemSlot)content;
                        ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
                        {
                            Kind = PresentationEventKind.ItemLooted,
                            Source = Entity.Null,
                            Target = itemSlot.ItemEntity,
                            Param = itemSlot.DataId
                        });
                    }
                }

                Debug.Log($"[ItemActions] Перенос #{sourceSlot} → #{targetSlot}");
                return;
            }

            // КЕЙС Б: целевой слот занят → своп
            var targetContent = target.GetContent(targetSlot);

            // 🎯 Пытаемся стакнуть
            if (TryToStack(ecb, source, sourceSlot, target, targetSlot, presentationBufferEntity))
            {
                return; // Стак успешен, своп не нужен
            }

            if (!source.CanPlaceContent(sourceSlot, targetContent))
            {
                Debug.LogWarning($"[ItemActions] Своп заблокирован: слот #{sourceSlot} не примет '{((ItemSlot)targetContent).DataId}'");
                return;
            }

            target.SetContent(targetSlot, content);
            source.SetContent(sourceSlot, targetContent);

            var playerEntSwap = PlayerUtils.GetEntityByTag<PlayerTag>(em);
            if (playerEntSwap != Entity.Null && em.HasComponent<BuffersLinkComponent>(playerEntSwap))
            {
                var links = em.GetComponentData<BuffersLinkComponent>(playerEntSwap);

                bool isSourceExternal = source.Entity != links.Inventory;
                bool isTargetMyBag = target.Entity == links.Inventory;

                if (isSourceExternal && isTargetMyBag)
                {
                    ItemSlot itemSlot = (ItemSlot)content;
                    ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
                    {
                        Kind = PresentationEventKind.ItemLooted,
                        Source = Entity.Null,
                        Target = itemSlot.ItemEntity,
                        Param = itemSlot.DataId
                    });
                }
                else if (!isSourceExternal && !isTargetMyBag && source.Entity == links.Inventory && target.Entity != links.Inventory)
                {
                    ItemSlot returnedItemSlot = (ItemSlot)targetContent;
                    ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
                    {
                        Kind = PresentationEventKind.ItemLooted,
                        Source = Entity.Null,
                        Target = returnedItemSlot.ItemEntity,
                        Param = returnedItemSlot.DataId
                    });
                }
            }

            Debug.Log($"[ItemActions] Своп #{sourceSlot} ↔ #{targetSlot}");
        }

        // ================================================================
        // DROP
        // ================================================================
        public static void Drop(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity containerEntity,
            int index,
            float3 position,
            Entity presentationBufferEntity)
        {
            if (!slotDataLookup.HasBuffer(containerEntity))
            {
                Debug.LogWarning("[ItemActions] У сущности нет буфера ItemSlot");
                return;
            }

            var slots = slotDataLookup[containerEntity];
            if (index < 0 || index >= slots.Length) return;

            var item = slots[index];
            if (item.IsEmpty) return;

            Entity itemEntity = item.ItemEntity;

            if (itemEntity == Entity.Null)
            {
                itemEntity = ecb.CreateEntity();
                ecb.AddComponent(itemEntity, new ItemComponent
                {
                    Uid = item.DataId.GetHashCode() + index,
                    ItemId = item.DataId,
                    Amount = item.Amount,
                });
                Debug.LogWarning($"⚠️ [ItemActions.Drop]: Entity.Null! Через ECB создана аварийная Entity для {item.DataId}.");
            }

            ClearSlot(slots, index);

            float3 dropPosition = position;
            dropPosition.y = PhysicsUtils.GetGroundHeight(dropPosition);

            ecb.AddComponent(itemEntity, LocalTransform.FromPosition(dropPosition));
            ecb.RemoveComponent<StoredTag>(itemEntity);

            ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
            {
                Kind = WorldEvents.ItemLooted,
                Target = itemEntity,
                Param = item.DataId
            });

            Debug.Log($"[ItemActions.Drop] {item.DataId} → {dropPosition}");
        }

        // ================================================================
        // SPLIT
        // ================================================================
        public static void Split(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity sourceEntity,
            int sourceSlot,
            int splitAmount,
            Entity targetEntity,
            int targetSlot,
            float3 dropPosition,
            Entity presentationBufferEntity)
        {
            if (!slotDataLookup.HasBuffer(sourceEntity)) return;
            if (splitAmount <= 0) return;

            var sourceSlots = slotDataLookup[sourceEntity];
            if (sourceSlot < 0 || sourceSlot >= sourceSlots.Length) return;

            var sourceSlotData = sourceSlots[sourceSlot];
            if (sourceSlotData.IsEmpty || sourceSlotData.Amount <= splitAmount) return;

            // 🎯 PRE-CHECK целевого слота ДО модификации источника
            bool targetIsWorld = targetEntity == Entity.Null;
            bool targetIsEmpty = false;
            bool targetIsSameItem = false;

            if (!targetIsWorld)
            {
                if (!slotDataLookup.HasBuffer(targetEntity)) return;
                var targetSlots = slotDataLookup[targetEntity];
                if (targetSlot < 0 || targetSlot >= targetSlots.Length) return;

                var targetSlotData = targetSlots[targetSlot];
                if (!targetSlotData.IsEmpty && targetSlotData.DataId != sourceSlotData.DataId)
                {
                    Debug.LogWarning("[ItemReducer.Split] Целевой слот занят другим предметом — отказ");
                    return;
                }

                targetIsEmpty = targetSlotData.IsEmpty;
                targetIsSameItem = !targetSlotData.IsEmpty;
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 1. Уменьшаем исходный слот
            int remaining = sourceSlotData.Amount - splitAmount;
            sourceSlotData.Amount = remaining;
            sourceSlots[sourceSlot] = sourceSlotData;

            // 2. Синхронизируем ItemComponent.Amount у исходной души
            Entity sourceSoul = sourceSlotData.ItemEntity;
            if (sourceSoul != Entity.Null && em.HasComponent<ItemComponent>(sourceSoul))
            {
                var soul = em.GetComponentData<ItemComponent>(sourceSoul);
                soul.Amount = remaining;
                em.SetComponentData(sourceSoul, soul);
            }

            // 🎯 MERGE: кладём в слот с таким же предметом — новую душу не создаём
            if (targetIsSameItem)
            {
                var targetSlots = slotDataLookup[targetEntity];
                var targetSlotData = targetSlots[targetSlot];
                int mergedAmount = targetSlotData.Amount + splitAmount;
                targetSlotData.Amount = mergedAmount;
                targetSlots[targetSlot] = targetSlotData;

                Entity targetSoul = targetSlotData.ItemEntity;
                if (targetSoul != Entity.Null && em.HasComponent<ItemComponent>(targetSoul))
                {
                    var soul = em.GetComponentData<ItemComponent>(targetSoul);
                    soul.Amount = mergedAmount;
                    em.SetComponentData(targetSoul, soul);
                }

                Debug.Log($"[ItemReducer.Split] {sourceSlotData.DataId} x{splitAmount} слито в слот #{targetSlot} (итого {mergedAmount})");
                return;
            }

            // 🎯 CREATE: создаём новую душу через ECB
            _splitUidCounter++;
            Entity newSoul = ecb.CreateEntity();
            ecb.AddComponent(newSoul, new ItemComponent
            {
                Uid = sourceSlotData.DataId.GetHashCode() + _splitUidCounter,
                ItemId = sourceSlotData.DataId,
                Amount = splitAmount
            });

            // 🎯 DROP В МИР
            if (targetIsWorld)
            {
                float3 groundPos = dropPosition;
                groundPos.y = PhysicsUtils.GetGroundHeight(groundPos);

                ecb.AddComponent(newSoul, LocalTransform.FromPosition(groundPos));
                // StoredTag НЕ добавляем — предмет на земле

                ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
                {
                    Kind = PresentationEventKind.ItemLooted,
                    Target = newSoul,
                    Param = sourceSlotData.DataId
                });

                Debug.Log($"[ItemReducer.Split] {sourceSlotData.DataId} x{splitAmount} → земля {groundPos}");
                return;
            }

            // 🎯 PUT В ПУСТОЙ СЛОТ
            if (targetIsEmpty)
            {
                var targetSlots = slotDataLookup[targetEntity];
                var targetSlotData = targetSlots[targetSlot];

                targetSlotData.DataId = sourceSlotData.DataId;
                targetSlotData.ContentType = sourceSlotData.ContentType;
                targetSlotData.Amount = splitAmount;
                targetSlotData.ItemEntity = newSoul;
                // SlotIndex, EquipSlot, ContainerType — паспорт целевого слота, не трогаем
                targetSlots[targetSlot] = targetSlotData;

                ecb.AddComponent<StoredTag>(newSoul);

                Debug.Log($"[ItemReducer.Split] {sourceSlotData.DataId} x{splitAmount} → пустой слот #{targetSlot}");
            }
        }

        // ================================================================
        // LOOT (из мира в слот)
        // ================================================================
        public static void Loot(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity itemEntity,
            Entity inventoryEntity,
            Entity presentationBufferEntity)
        {
            if (!slotDataLookup.HasBuffer(inventoryEntity)) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<ItemComponent>(itemEntity)) return;
            if (em.HasComponent<StoredTag>(itemEntity)) return;

            var itemData = em.GetComponentData<ItemComponent>(itemEntity);
            var slots = slotDataLookup[inventoryEntity];

            int targetSlotIndex = FindSlotForItem(slots, itemData.ItemId, out bool isStacking);
            if (targetSlotIndex == -1) return; // Инвентарь полон

            if (isStacking)
            {
                var config = ItemsDatabase.GetItem(itemData.ItemId.ToString());
                int space = config.properties.max_stack - slots[targetSlotIndex].Amount;
                int amountToAdd = math.min(itemData.Amount, space);

                ApplyStack(ecb, ref slotDataLookup, inventoryEntity, targetSlotIndex, itemData, itemEntity, amountToAdd);

                Debug.Log($"[ItemActions.Loot] {itemData.ItemId} x{amountToAdd} добавлено в стак (Слот {targetSlotIndex})");
            }
            else
            {
                slots[targetSlotIndex] = new ItemSlot
                {
                    SlotIndex = targetSlotIndex,
                    DataId = itemData.ItemId,
                    ContentType = SlotContentType.Item,
                    Amount = itemData.Amount,
                    ItemEntity = itemEntity,
                    EquipSlot = EquipSlot.NONE,
                    ContainerType = ContainerType.INVENTORY
                };

                ecb.AddComponent<StoredTag>(itemEntity);
                if (em.HasComponent<VisualizedTag>(itemEntity)) ecb.RemoveComponent<VisualizedTag>(itemEntity);

                Debug.Log($"[ItemActions.Loot] {itemData.ItemId} (Entity {itemEntity.Index}) упакован в рюкзак.");
            }

            ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
            {
                Kind = WorldEvents.ItemLooted,
                Source = inventoryEntity,
                Target = itemEntity,
                Param = itemData.ItemId
            });
        }

        // ================================================================
        // TAKE ALL (кнопка "Взять всё" из контейнера)
        // ================================================================
        public static void TakeAll(
            ref BufferLookup<ItemSlot> slotDataLookup,
            EntityCommandBuffer ecb,
            Entity sourceBagEntity,
            Entity targetBagEntity,
            Entity presentationBufferEntity)
        {
            if (!slotDataLookup.HasBuffer(sourceBagEntity) || !slotDataLookup.HasBuffer(targetBagEntity)) return;

            var sourceSlots = slotDataLookup[sourceBagEntity];
            var targetSlots = slotDataLookup[targetBagEntity];
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            for (int i = 0; i < sourceSlots.Length; i++)
            {
                var sourceSlot = sourceSlots[i];
                if (sourceSlot.IsEmpty) continue;

                var config = ItemsDatabase.GetItem(sourceSlot.DataId.ToString());
                bool isStacked = false;

                // 🎯 1. Пытаемся стакнуть в существующие слоты
                if (config != null && config.properties.stackable && config.properties.max_stack > 0)
                {
                    for (int j = 0; j < targetSlots.Length; j++)
                    {
                        if (!targetSlots[j].IsEmpty && targetSlots[j].DataId == sourceSlot.DataId)
                        {
                            int space = config.properties.max_stack - targetSlots[j].Amount;
                            if (space > 0)
                            {
                                int toAdd = math.min(sourceSlot.Amount, space);

                                var targetSlot = targetSlots[j];
                                targetSlot.Amount += toAdd;
                                targetSlots[j] = targetSlot;

                                sourceSlot.Amount -= toAdd;
                                sourceSlots[i] = sourceSlot;

                                // Синхронизация целевой души
                                if (targetSlot.ItemEntity != Entity.Null && em.HasComponent<ItemComponent>(targetSlot.ItemEntity))
                                {
                                    var soul = em.GetComponentData<ItemComponent>(targetSlot.ItemEntity);
                                    soul.Amount = targetSlot.Amount;
                                    em.SetComponentData(targetSlot.ItemEntity, soul);
                                }

                                // Если исходный опустел
                                if (sourceSlot.Amount <= 0)
                                {
                                    if (sourceSlot.ItemEntity != Entity.Null) ecb.DestroyEntity(sourceSlot.ItemEntity);
                                    sourceSlot.ClearContent();
                                    sourceSlots[i] = sourceSlot;
                                    isStacked = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (isStacked || sourceSlot.IsEmpty) continue;

                // 🎯 2. Пытаемся найти пустой слот для остатка
                int freeSlotIndex = -1;
                for (int j = 0; j < targetSlots.Length; j++)
                {
                    if (targetSlots[j].IsEmpty) { freeSlotIndex = j; break; }
                }

                if (freeSlotIndex != -1)
                {
                    var targetSlot = targetSlots[freeSlotIndex];
                    targetSlot.DataId = sourceSlot.DataId;
                    targetSlot.ContentType = sourceSlot.ContentType;
                    targetSlot.Amount = sourceSlot.Amount;
                    targetSlot.ItemEntity = sourceSlot.ItemEntity;
                    targetSlots[freeSlotIndex] = targetSlot;

                    if (sourceSlot.ItemEntity != Entity.Null)
                    {
                        ecb.AddComponent<StoredTag>(sourceSlot.ItemEntity);
                        if (em.HasComponent<VisualizedTag>(sourceSlot.ItemEntity)) 
                            ecb.RemoveComponent<VisualizedTag>(sourceSlot.ItemEntity);
                    }

                    sourceSlot.ClearContent();
                    sourceSlots[i] = sourceSlot;
                }
                else
                {
                    // 🎯 3. Нет места — оставляем в сундуке и идём к следующему
                    Debug.LogWarning($"[ItemReducer.TakeAll] Нет места для {sourceSlot.DataId} в рюкзаке, оставлен в сундуке.");
                }
            }

            Debug.Log($"💰 [ItemReducer.TakeAll]: Перелиты предметы из {sourceBagEntity.Index} в {targetBagEntity.Index}.");
        }

        // ================================================================
        // USE
        // ================================================================
        public static void Use(
            EntityCommandBuffer ecb,
            ref BufferLookup<ItemSlot> slotDataLookup,
            Entity containerEntity,
            int index,
            string itemId,
            Entity casterEntity)
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
                ApplyItemEffect(effect, casterEntity, config);
            }

            if (config.properties.stackable && config.properties.max_stack > 0)
            {
                if (!slotDataLookup.HasBuffer(containerEntity)) return;

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
                        ContentType = SlotContentType.Item,
                        Amount = item.Amount - 1,
                        EquipSlot = item.EquipSlot,
                        ItemEntity = item.ItemEntity
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
        // CREATE (из воздуха в слот)
        // ================================================================
        public static void Create(
            EntityCommandBuffer ecb,
            ref BufferLookup<ItemSlot> slotDataLookup,
            Entity inventoryEntity,
            string itemId,
            int amount,
            Entity presentationBufferEntity)
        {
            if (!slotDataLookup.HasBuffer(inventoryEntity))
            {
                Debug.LogWarning($"[ItemReducer.Create] У {inventoryEntity.Index} нет ItemSlot");
                return;
            }

            var slots = slotDataLookup[inventoryEntity];
            int targetSlotIndex = FindSlotForItem(slots, itemId, out bool isStacking);

            if (targetSlotIndex == -1)
            {
                Debug.LogWarning($"[ItemReducer.Create] Нет свободного слота для '{itemId}'");
                return;
            }

            if (isStacking)
            {
                var config = ItemsDatabase.GetItem(itemId);
                int space = config.properties.max_stack - slots[targetSlotIndex].Amount;
                int amountToAdd = math.min(amount, space);

                var sourceItemComponent = new ItemComponent
                {
                    Uid = itemId.GetHashCode(),
                    ItemId = itemId,
                    Amount = amountToAdd
                };

                ApplyStack(ecb, ref slotDataLookup, inventoryEntity, targetSlotIndex, sourceItemComponent, Entity.Null, amountToAdd);

                if (amountToAdd < amount)
                {
                    Create(ecb, ref slotDataLookup, inventoryEntity, itemId, amount - amountToAdd, presentationBufferEntity);
                }

                Debug.Log($"[ItemReducer.Create] {itemId} x{amountToAdd} добавлено в стак (Слот {targetSlotIndex})");
            }
            else
            {
                Entity itemEntity = ecb.CreateEntity();
                ecb.AddComponent(itemEntity, new ItemComponent
                {
                    Uid = itemId.GetHashCode() + targetSlotIndex,
                    ItemId = itemId,
                    Amount = amount
                });

                var slot = slots[targetSlotIndex];
                slot.SlotIndex = targetSlotIndex;
                slot.DataId = itemId;
                slot.ContentType = SlotContentType.Item;
                slot.Amount = amount;
                slot.ItemEntity = itemEntity;
                slot.EquipSlot = EquipSlot.NONE;
                slot.ContainerType = ContainerType.INVENTORY;
                slots[targetSlotIndex] = slot;

                ecb.AppendToBuffer(presentationBufferEntity, new PresentationEvent
                {
                    Kind = PresentationEventKind.ItemLooted,
                    Source = inventoryEntity,
                    Target = itemEntity,
                    Param = itemId
                });

                Debug.Log($"[ItemReducer.Create] '{itemId}' x{amount} → слот {targetSlotIndex}");
            }
        }

        // ================================================================
        // ХЕЛПЕР: ПОИСК СЛОТА (сначала стак, потом пустой)
        // ================================================================
        private static int FindSlotForItem(DynamicBuffer<ItemSlot> slots, FixedString32Bytes itemId, out bool isStacking)
        {
            isStacking = false;
            var config = ItemsDatabase.GetItem(itemId.ToString());

            // 1. Ищем слот для стака
            if (config != null && config.properties.stackable && config.properties.max_stack > 0)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsEmpty && slots[i].DataId == itemId)
                    {
                        if (slots[i].Amount < config.properties.max_stack)
                        {
                            isStacking = true;
                            return i;
                        }
                    }
                }
            }

            // 2. Ищем пустой слот
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) return i;
            }

            return -1; // Нет места
        }

        // ================================================================
        // TRY-TO-STACK (Вызывается из Transfer перед свопом)
        // ================================================================
        public static bool TryToStack(
            EntityCommandBuffer ecb,
            ISlotContainer source,
            int sourceSlot,
            ISlotContainer target,
            int targetSlot,
            Entity presentationBufferEntity)
        {
            // 1. Нельзя стакать в тот же самый слот
            if (source.Entity == target.Entity && sourceSlot == targetSlot) return false;

            // 2. Проверяем, что оба слота не пустые
            if (!source.HasContent(sourceSlot) || !target.HasContent(targetSlot)) return false;

            // 3. Приводим контент к ItemSlot
            if (source.GetContent(sourceSlot) is not ItemSlot sourceItem) return false;
            if (target.GetContent(targetSlot) is not ItemSlot targetItem) return false;

            // 4. Проверяем, что это один и тот же предмет
            string sourceItemId = sourceItem.DataId.ToString();
            string targetItemId = targetItem.DataId.ToString();

            if (string.IsNullOrEmpty(sourceItemId) || sourceItemId != targetItemId) return false;

            // 5. Проверяем, что предмет можно стакать и есть ли место
            var config = ItemsDatabase.GetItem(sourceItemId);
            if (config == null || !config.properties.stackable || config.properties.max_stack <= 0) return false;

            int totalAmount = sourceItem.Amount + targetItem.Amount;
            if (totalAmount > config.properties.max_stack) return false;

            // ================================================================
            // ВЫПОЛНЯЕМ СТАК
            // ================================================================
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            targetItem.Amount = totalAmount;
            target.SetContent(targetSlot, targetItem);
            source.ClearSlot(sourceSlot);

            if (targetItem.ItemEntity != Entity.Null && em.HasComponent<ItemComponent>(targetItem.ItemEntity))
            {
                var targetSoul = em.GetComponentData<ItemComponent>(targetItem.ItemEntity);
                targetSoul.Amount = totalAmount;
                em.SetComponentData(targetItem.ItemEntity, targetSoul);
            }

            if (sourceItem.ItemEntity != Entity.Null && sourceItem.ItemEntity != targetItem.ItemEntity)
            {
                ecb.DestroyEntity(sourceItem.ItemEntity);
            }

            Debug.Log($"[ItemActions] Стак: {sourceItemId} x{sourceItem.Amount} + x{targetItem.Amount} = x{totalAmount} (Слот #{sourceSlot} → #{targetSlot})");
            return true;
        }

        // ================================================================
        // ОБЩИЙ ХЕЛПЕР ДЛЯ СТАКА (Из мира в слот, или из воздуха в слот)
        // ================================================================
        private static void ApplyStack(
            EntityCommandBuffer ecb,
            ref BufferLookup<ItemSlot> slotDataLookup,
            Entity targetEntity,
            int targetSlot,
            ItemComponent sourceItemComponent,
            Entity sourceItemEntity,
            int amountToAdd)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var slots = slotDataLookup[targetEntity];
            var targetSlotData = slots[targetSlot];

            targetSlotData.Amount += amountToAdd;
            slots[targetSlot] = targetSlotData;

            if (targetSlotData.ItemEntity != Entity.Null && em.HasComponent<ItemComponent>(targetSlotData.ItemEntity))
            {
                var targetSoul = em.GetComponentData<ItemComponent>(targetSlotData.ItemEntity);
                targetSoul.Amount = targetSlotData.Amount;
                em.SetComponentData(targetSlotData.ItemEntity, targetSoul);
            }

            if (sourceItemEntity == Entity.Null) return; // Create — нет исходной сущности

            if (amountToAdd >= sourceItemComponent.Amount)
            {
                ecb.DestroyEntity(sourceItemEntity);
            }
            else
            {
                var updatedSource = em.GetComponentData<ItemComponent>(sourceItemEntity);
                updatedSource.Amount -= amountToAdd;
                em.SetComponentData(sourceItemEntity, updatedSource);
            }
        }

        // ================================================================
        // ЭФФЕКТЫ
        // ================================================================
        private static void ApplyItemEffect(ItemUseEffect effect, Entity casterEntity, ItemConfig config)
        {
            if (effect.ability_id == "heal") { Debug.Log($"[ItemActions] Лечение {effect.value?.min}-{effect.value?.max} HP для {casterEntity}"); }
            else if (effect.ability_id == "mana") { Debug.Log($"[ItemActions] Мана {effect.value?.min}-{effect.value?.max} для {casterEntity}"); }
            else if (effect.ability_id == "buff") { Debug.Log($"[ItemActions] Бафф на {casterEntity}"); }
            else if (effect.ability_id == "summon") { Debug.Log($"[ItemActions] Призыв для {casterEntity}"); }
            else { Debug.Log($"[ItemActions] Эффект {effect.ability_id} → {casterEntity}"); }
        }
    }
}

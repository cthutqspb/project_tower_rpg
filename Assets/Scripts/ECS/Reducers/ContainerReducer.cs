using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.Core.Loot;

namespace ProjectTowerRpg.ECS.Reducers
{
    public static class ContainerReducer
    {
        public static void Open(Entity containerEntity, EntityCommandBuffer ecb)
        {
            if (containerEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!em.HasComponent<ItemComponent>(containerEntity))
            {
                Debug.LogWarning($"[ContainerActions] {containerEntity.Index} не имеет ItemComponent!");
                return;
            }

            // 🦾 СВЕРХЗВУКОВОЙ ТРЕКИНГ МЕШКА: Прямой адрес из ОЗУ чанка вместо ContainerHelper!
            Entity bagEntity = Entity.Null;

            if (em.HasComponent<BuffersLinkComponent>(containerEntity))
            {
                bagEntity = em.GetComponentData<BuffersLinkComponent>(containerEntity).Inventory;
            }

            // Если мешка нет или он стерт — шёлково запускаем твой генератор лута
            if (bagEntity == Entity.Null || !em.Exists(bagEntity))
            {
                CreateBag(containerEntity, ecb);
                Debug.Log($"[ContainerActions] Создан мешок для {containerEntity.Index} через ECB");
            }
            else
            {
                Debug.Log($"[ContainerActions] Мешок для {containerEntity.Index} уже существует");
            }

            // ================================================================
            // 🦾 ВЗАИМОДЕЙСТВИЕ В БУФЕР ИГРОКА (По твоему канону!)
            // ================================================================
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                ecb.AppendToBuffer(playerEntity, new InteractionEntry
                {
                    TargetEntity = containerEntity,
                    MaxDistance = 1.5f
                });
                Debug.Log($"[ContainerActions] Добавлено взаимодействие с {containerEntity.Index} в буфер игрока");
            }

            UIEvents.TriggerOpenContainerWindow(containerEntity);
        }

        private static void CreateBag(Entity containerEntity, EntityCommandBuffer ecb)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            if (!em.HasComponent<ItemComponent>(containerEntity))
                return;

            var itemComp = em.GetComponentData<ItemComponent>(containerEntity);
            var itemConfig = ItemsDatabase.GetItem(itemComp.ItemId.ToString());

            if (itemConfig == null || itemConfig.identity.type != "container")
                return;

            int columns = itemConfig.properties.columns ?? 6;
            int rows = itemConfig.properties.rows ?? 4;
            int totalSlots = columns * rows;

            // 🔥 ГЕНЕРАЦИЯ ЛУТА ЧЕРЕЗ LootService
            var lootItems = LootService.GenerateLoot(itemComp.LootTableId.ToString());

            // ✅ СОЗДАЁМ МЕШОК ЧЕРЕЗ ECB
            Entity bagEntity = ecb.CreateEntity();

            ecb.AddComponent(bagEntity, new ContainerConfigComponent
            {
                Owner = containerEntity,
                Columns = columns,
                Rows = rows
            });
            
            ecb.AddComponent<ContainerTag>(bagEntity);
            ecb.AddComponent<InventoryTag>(bagEntity);

            var slotsBuffer = ecb.AddBuffer<ItemSlot>(bagEntity);
            for (int i = 0; i < totalSlots; i++)
            {
                slotsBuffer.Add(new ItemSlot { SlotIndex = i, ContainerType = ContainerType.INVENTORY });
            }

            // Раскладываем лут
            int slotIndex = 0;
            foreach (var item in lootItems)
            {
                if (slotIndex >= totalSlots) break;
                slotsBuffer[slotIndex] = new ItemSlot
                {
                    SlotIndex = slotIndex,
                    DataId = item.ItemId,
                    DataType = "item",
                    Amount = item.Amount,
                    ContainerType = ContainerType.INVENTORY
                };
                slotIndex++;
            }

            // 🦾 ЗАПЕКАЕМ СВЯЗЬ: Сундук намертво запоминает свой мешок за 0 наносекунд!
            ecb.AddComponent(containerEntity, new BuffersLinkComponent
            {
                Inventory = bagEntity,
                Paperdoll = Entity.Null
            });

            // ПОМЕЧАЕМ КАК ОБЛУТАННЫЙ
            itemComp.IsLooted = true;
            ecb.SetComponent(containerEntity, itemComp);
        }
    }
}


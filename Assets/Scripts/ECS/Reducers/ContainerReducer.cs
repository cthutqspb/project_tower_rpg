using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.Core.Loot;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Reducers
{
    public static class ContainerReducer
    {
        public static void Open(Entity containerEntity, EntityCommandBuffer ecb)
        {
            if (containerEntity == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 🦾 Резолв bagEntity через линк (модель C)
            if (!em.HasComponent<BuffersLinkComponent>(containerEntity))
            {
                Debug.LogWarning($"[ContainerReducer] У {containerEntity.Index} нет BuffersLinkComponent — открывать нечего");
                return;
            }

            var linkedEntitites = em.GetComponentData<BuffersLinkComponent>(containerEntity);
            Entity bagEntity = linkedEntitites.Inventory;

            if (bagEntity == Entity.Null || !em.Exists(bagEntity))
            {
                Debug.LogWarning($"[ContainerReducer] У {containerEntity.Index} нет валидного Inventory");
                return;
            }

            // ================================================================
            // 🎲 ГЕНЕРАЦИЯ ЛУТА ИЗ ТАБЛИЦЫ ПРИ ПЕРВОМ ОТКРЫТИИ
            // ================================================================
            if (!em.HasComponent<LootGeneratedTag>(containerEntity))
            {
                FixedString64Bytes lootTableId = default;

                if (em.HasComponent<ItemComponent>(containerEntity))
                {
                    lootTableId = em.GetComponentData<ItemComponent>(containerEntity).LootTableId;
                }
                // TODO: UnitComponent.LootTableId — когда добавишь, раскомментируй:
                // else if (em.HasComponent<UnitComponent>(containerEntity))
                // {
                //     lootTableId = em.GetComponentData<UnitComponent>(containerEntity).LootTableId;
                // }

                if (!lootTableId.IsEmpty)
                {
                    var dynamicLoot = LootService.GenerateLoot(lootTableId.ToString());
                    var slotsBuffer = em.GetBuffer<ItemSlot>(bagEntity);

                    int filled = 0;
                    for (int i = 0; i < slotsBuffer.Length && filled < dynamicLoot.Count; i++)
                    {
                        if (!slotsBuffer[i].DataId.IsEmpty) continue;

                        var loot = dynamicLoot[filled];
                        slotsBuffer[i] = new ItemSlot
                        {
                            SlotIndex = i,
                            DataId = loot.ItemId,
                            DataType = "item",
                            Amount = loot.Amount,
                            ContainerType = ContainerType.INVENTORY,
                        };
                        filled++;
                    }

                    Debug.Log($"[ContainerReducer] Сгенерировано {filled} предметов из таблицы {lootTableId}");
                }

                // 🦾 ЕДИНАЯ ТОЧКА: тег через ECB (structural change!)
                ecb.AddComponent<LootGeneratedTag>(containerEntity);
            }

            // ================================================================
            // 🦾 ВЗАИМОДЕЙСТВИЕ В БУФЕР ИГРОКА
            // ================================================================
            Entity playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
            if (playerEntity != Entity.Null)
            {
                ecb.AppendToBuffer(playerEntity, new InteractionEntry
                {
                    TargetEntity = containerEntity,
                    MaxDistance = 1.5f,
                });
            }

            // 🚀 Открываем окно контейнера
            UIEvents.TriggerOpenWindow(WindowType.Container , containerEntity);
        }
    }
}

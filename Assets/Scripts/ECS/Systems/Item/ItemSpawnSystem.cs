using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.Core.Items;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemSpawnSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // ================================================================
            // СЦЕНАРНЫЕ ПРЕДМЕТЫ (ItemSpawnMarkerComponent)
            // ================================================================
            var markerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ItemSpawnMarkerComponent>());
            if (!markerQuery.IsEmpty)
            {
                var markers = markerQuery.ToEntityArray(Allocator.Temp);
                foreach (var markerEntity in markers)
                {
                    var markerData = em.GetComponentData<ItemSpawnMarkerComponent>(markerEntity);

                    float3 spawnPosition = markerData.SpawnPosition;
                    spawnPosition.y = PhysicsUtils.GetGroundHeight(spawnPosition);

                    string itemIdStr = markerData.ItemId.ToString();
                    int generatedUidHash = ($"i_{(int)spawnPosition.x}_{(int)spawnPosition.z}").GetHashCode();

                    Entity itemEntity = em.CreateEntity();
                    em.AddComponentData(itemEntity, LocalTransform.FromPosition(spawnPosition));
                    em.AddComponentData(itemEntity, new ItemComponent
                    {
                        Uid = generatedUidHash,
                        ItemId = markerData.ItemId,
                        Amount = markerData.Amount,
                        LootTableId = markerData.LootTableId,
                        RespawnTime = markerData.RespawnTime,
                    });

                    var itemCfg = ItemsDatabase.GetItem(itemIdStr);
                    bool isContainer = itemCfg != null && itemCfg.identity.type == "container";

                    if (isContainer)
                    {
                        int columns = itemCfg.properties.columns ?? 6;
                        int rows = itemCfg.properties.rows ?? 4;
                        int totalSlots = columns * rows;

                        Entity bagEntity = em.CreateEntity();
                        em.AddComponentData(bagEntity, new ContainerConfigComponent
                        {
                            Owner = itemEntity,
                            Columns = columns,
                            Rows = rows,
                        });
                        em.AddComponent<InventoryTag>(bagEntity);

                        var slotsBuffer = em.AddBuffer<ItemSlot>(bagEntity);
                        for (int i = 0; i < totalSlots; i++)
                        {
                            slotsBuffer.Add(new ItemSlot
                            {
                                SlotIndex = i,
                                DataId = "",
                                DataType = "",
                                Amount = 0,
                                ContainerType = ContainerType.INVENTORY,
                            });
                        }

                        if (em.HasBuffer<ItemSlot>(markerEntity))
                        {
                            var customItems = em.GetBuffer<ItemSlot>(markerEntity).ToNativeArray(Allocator.Temp);
                            int slotsToFill = math.min(customItems.Length, totalSlots);
                            for (int i = 0; i < slotsToFill; i++)
                            {
                                slotsBuffer[i] = new ItemSlot
                                {
                                    SlotIndex = i,
                                    DataId = customItems[i].DataId,
                                    DataType = "item",
                                    Amount = customItems[i].Amount,
                                    ContainerType = ContainerType.INVENTORY,
                                };
                            }
                            customItems.Dispose();
                        }

                        em.AddComponentData(itemEntity, new BuffersLinkComponent
                        {
                            Inventory = bagEntity,
                            Paperdoll = Entity.Null,
                        });
                        em.AddComponent<ContainerTag>(itemEntity);

                        Debug.Log($"[ItemSpawnSystem] Контейнер {itemIdStr} создан с bagEntity {bagEntity.Index}");
                    }

                    em.DestroyEntity(markerEntity);
                }
                markers.Dispose();
            }
        }
    }
}

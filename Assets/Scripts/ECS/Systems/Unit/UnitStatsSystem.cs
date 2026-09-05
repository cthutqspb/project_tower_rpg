using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class UnitStatsSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            var itemSlotLookup = SystemAPI.GetBufferLookup<ItemSlot>(true);

            // Кверим плоские чанки через твой новый BuffersLinkComponent
            foreach (var (baseAttributes, currentAttributes, health, links, entity) in 
                     SystemAPI.Query<RefRO<UnitBaseAttributesComponent>, RefRW<UnitCurrentAttributesComponent>, RefRO<HealthComponent>, RefRO<BuffersLinkComponent>>()
                     .WithEntityAccess())
            {
                Entity paperdollEntity = links.ValueRO.Paperdoll;
                Entity inventoryEntity = links.ValueRO.Inventory;

                // Нативный трекинг версий чанков без костыльных ивентов
                bool gearChanged = paperdollEntity != Entity.Null && 
                                  itemSlotLookup.HasBuffer(paperdollEntity) && 
                                  itemSlotLookup.DidChange(paperdollEntity, LastSystemVersion);

                bool inventoryChanged = inventoryEntity != Entity.Null && 
                                      itemSlotLookup.HasBuffer(inventoryEntity) && 
                                      itemSlotLookup.DidChange(inventoryEntity, LastSystemVersion);

                if (!gearChanged && !inventoryChanged) continue;

                // Точки отсчета из локального стека процессора
                int totalStrength = baseAttributes.ValueRO.strength;
                int totalAgility = baseAttributes.ValueRO.agility;
                int totalIntellect = baseAttributes.ValueRO.intellect;
                int totalWisdom = baseAttributes.ValueRO.wisdom;
                int totalStamina = baseAttributes.ValueRO.stamina;

                // 1. СБОР СТАТ СО ШМОТОК НА КУКЛЕ (PAPERDOLL) — Считаем всё подряд!
                if (paperdollEntity != Entity.Null && itemSlotLookup.HasBuffer(paperdollEntity))
                {
                    var slotsBuffer = em.GetBuffer<ItemSlot>(paperdollEntity, true);
                    foreach (var slot in slotsBuffer)
                    {
                        if (slot.IsEmpty || slot.ContainerType != ContainerType.PAPERDOLL) continue;

                        string itemIdStr = slot.DataId.ToString().ToLower().Trim();
                        ItemConfig itemConfig = ItemsDatabase.GetItem(itemIdStr);
                        
                        if (itemConfig != null && itemConfig.combat_stats?.attributes != null)
                        {
                            var itemAttr = itemConfig.combat_stats.attributes;
                            totalStrength += itemAttr.strength;
                            totalIntellect += itemAttr.intellect;
                            totalAgility += itemAttr.agility;
                            totalStamina += itemAttr.stamina;
                            totalWisdom += itemAttr.wisdom;
                        }
                    }
                }

                // 2. 🦾 ИСПРАВЛЕНО НАМЕРТВО: СБОР СТАТ ИЗ РЮКЗАКА (INVENTORY)
                // Обычный шмот игнорируем! Плюсуем только если это ПАССИВНЫЙ ТАЛИСМАН/АМУЛЕТ!
                if (inventoryEntity != Entity.Null && itemSlotLookup.HasBuffer(inventoryEntity))
                {
                    var slotsBuffer = em.GetBuffer<ItemSlot>(inventoryEntity, true);
                    foreach (var slot in slotsBuffer)
                    {
                        if (slot.IsEmpty || slot.ContainerType != ContainerType.INVENTORY) continue;

                        string itemIdStr = slot.DataId.ToString().ToLower().Trim();
                        ItemConfig itemConfig = ItemsDatabase.GetItem(itemIdStr);
                        
                        // Проверяем ММО-флаг пассивки из твоего конфига (например, .is_passive или по типу предмета)
                        // Если такого флага пока нет, временно закомментируй этот блок или оставь проверку на будущее,
                        // чтобы обычные мечи из рюкзака БОЛЬШЕ НЕ ПЛЮСОВАЛИСЬ ложно к силе персонажа!
                        // if (itemConfig != null && itemConfig.is_passive_charm && itemConfig.combat_stats?.attributes != null)
                        // {
                        //     var itemAttr = itemConfig.combat_stats.attributes;
                        //     totalStrength += itemAttr.strength;
                        //     totalIntellect += itemAttr.intellect;
                        //     totalAgility += itemAttr.agility;
                        //     totalStamina += itemAttr.stamina;
                        //     totalWisdom += itemAttr.wisdom;
                        // }
                    }
                }

                // Запекаем легитимный Си-паспорт стат в ОЗУ чанка персонажа
                var rwAttributes = currentAttributes.ValueRW;
                rwAttributes.strength = totalStrength;
                rwAttributes.agility = totalAgility;
                rwAttributes.intellect = totalIntellect;
                rwAttributes.wisdom = totalWisdom;
                rwAttributes.stamina = totalStamina;
                currentAttributes.ValueRW = rwAttributes;

                // Пересчитываем Макс ХП
                int calculatedMaxHealth = 100 + (totalStamina * 10); 
                var hpState = health.ValueRO;
                
                if (hpState.Max != calculatedMaxHealth)
                {
                    float hpPercent = hpState.Max > 0 ? (hpState.Current / hpState.Max) : 1f;
                    hpState.Max = calculatedMaxHealth;
                    hpState.Current = math.clamp(calculatedMaxHealth * hpPercent, 0f, calculatedMaxHealth);

                    var healthRW = SystemAPI.GetComponentRW<HealthComponent>(entity);
                    healthRW.ValueRW = hpState;
                }

                Debug.Log($"⚙️ [UnitStatsSystem]: Сверхзвуковой пересчет завершен! Сила: {totalStrength}, ХП: {calculatedMaxHealth}");
            }
        }
    }
}


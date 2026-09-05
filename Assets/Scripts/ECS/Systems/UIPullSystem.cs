using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UIPullSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 🔍 1. БЫСТРЫЕ, ПОТОКОБЕЗОПАСНЫЕ ЛУКАПЫ КОМПОНЕНТОВ (Чтение)
            var slotLookup = SystemAPI.GetBufferLookup<ItemSlot>(true);
            var actionBarLookup = SystemAPI.GetBufferLookup<ActionBarSlot>(true);
            var healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
            var resourceLookup = SystemAPI.GetComponentLookup<ResourceComponent>(true);
            var unitLookup = SystemAPI.GetComponentLookup<UnitComponent>(true);

            var combatLookup = SystemAPI.GetComponentLookup<CombatStateComponent>(true);

            var unitCurrentAttributesLookup = SystemAPI.GetComponentLookup<UnitCurrentAttributesComponent>(true);
            var unitCombatStatsLookup = SystemAPI.GetComponentLookup<UnitCombatStatsComponent>(true);

            var castLookup = SystemAPI.GetComponentLookup<CastComponent>(true);

            // 🔄 2. ЕДИНЫЙ ЦИКЛ ПО ВСЕМУ АКТИВНОМУ ИНТЕРФЕЙСУ ИГРЫ
            foreach (var entity in UIRegistry.GetActiveEntities())
            {
                var receivers = UIRegistry.GetReceivers(entity);
                if (receivers == null) continue;

                // ================================================================
                // СЛАЙС А: Твой исходный рабочий код для Инвентаря / Куклы персонажа
                // ================================================================
                if (slotLookup.HasBuffer(entity) && slotLookup.DidChange(entity, LastSystemVersion))
                {
                    var slots = slotLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<ItemSlot> ui)
                        {
                            ui.UpdateFromBuffer(slots, false);
                        }
                    }
                }

                if (actionBarLookup.HasBuffer(entity) && actionBarLookup.DidChange(entity, LastSystemVersion))
                {
                    var barSlots = actionBarLookup[entity];
                    
                    foreach (var receiver in receivers)
                    {
                        // Ищем сетки, готовые принять и отрендерить новый буфер ярлыков!
                        if (receiver is IEcsUiBufferReceiver<ActionBarSlot> barUi)
                        {
                            barUi.UpdateFromBuffer(barSlots, false);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Б: РЕАКТИВНОЕ ОБНОВЛЕНИЕ ЗДОРОВЬЯ ЮНИТА (UnitFrame)
                // ================================================================
                if (healthLookup.HasComponent(entity) && healthLookup.DidChange(entity, LastSystemVersion))
                {
                    var healthData = healthLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<HealthComponent> healthUi)
                        {
                            healthUi.UpdateFromComponent(ref healthData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС В: РЕАКТИВНОЕ ОБНОВЛЕНИЕ РЕСУРСА ЮНИТА (UnitFrame)
                // ================================================================
                if (resourceLookup.HasComponent(entity) && resourceLookup.DidChange(entity, LastSystemVersion))
                {   
                    var resourceData = resourceLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<ResourceComponent> resourceUi)
                        {   
                            resourceUi.UpdateFromComponent(ref resourceData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Г: РЕАКТИВНОЕ ОБНОВЛЕНИЕ ЮНИТА (UnitFrame)
                // ================================================================
                if (unitLookup.HasComponent(entity) && unitLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitData = unitLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitComponent> unitUi)
                        {   
                            unitUi.UpdateFromComponent(ref unitData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Д: РЕАКТИВНОЕ ОБНОВЛЕНИЕ ТЕКУЩИХ АТРИБУТОВ ЮНИТА (UnitStats)
                // ================================================================
                if (unitCurrentAttributesLookup.HasComponent(entity) && unitCurrentAttributesLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitCurrentAttributesData = unitCurrentAttributesLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitCurrentAttributesComponent> unitStatsUi)
                        {   
                            unitStatsUi.UpdateFromComponent(ref unitCurrentAttributesData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС E: РЕАКТИВНОЕ ОБНОВЛЕНИЕ СТАТОВ ЮНИТА (UnitStats)
                // ================================================================
                if (unitCombatStatsLookup.HasComponent(entity) && unitCombatStatsLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitCombatStatsData = unitCombatStatsLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitCombatStatsComponent> unitStatsUi)
                        {   
                            unitStatsUi.UpdateFromComponent(ref unitCombatStatsData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Ж: РЕАКТИВНОЕ ОБНОВЛЕНИЕ КАСTА ЮНИТА (Wow-Канон)
                // ================================================================
                if (castLookup.HasComponent(entity))
                {
                    var castData = castLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<CastComponent> castUi)
                        {
                            castUi.UpdateFromComponent(ref castData);
                        }
                    }
                }
            }
        }
    }
}


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
            var slotLookup = SystemAPI.GetBufferLookup<SlotData>(true);
            var actionBarLookup = SystemAPI.GetBufferLookup<ActionBarSlot>(true);
            var healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
            var resourceLookup = SystemAPI.GetComponentLookup<ResourceComponent>(true);
            var unitLookup = SystemAPI.GetComponentLookup<UnitComponent>(true);

            var combatLookup = SystemAPI.GetComponentLookup<CombatStateComponent>(true);

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
                        if (receiver is IEcsUiBufferReceiver<SlotData> ui)
                        {
                            ui.UpdateFromBuffer(slots);
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
                            barUi.UpdateFromBuffer(barSlots);
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
            }
        }
    }
}


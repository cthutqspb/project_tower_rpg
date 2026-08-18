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
            var healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
            var resourceLookup = SystemAPI.GetComponentLookup<ResourceComponent>(true);

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
                // 🌟 ТВОЙ СЛАЙС Г: ОБНОВЛЕНИЕ ФРЕЙМА ЦЕЛИ (TargetFrame)
                // ================================================================
                // entity здесь — это Игрок, к которому привязан TargetFrame в UIRegistry
                if (combatLookup.HasComponent(entity))
                {
                    var combatState = combatLookup[entity];
                    Entity targetEntity = combatState.CurrentTarget;

                    // Если цель есть и она существует в мире
                    if (targetEntity != Entity.Null && healthLookup.HasComponent(targetEntity))
                    {
                        // Проверяем: изменился ли сам таргет ИЛИ изменились ли данные внутри этого таргета
                        bool targetChanged = combatLookup.DidChange(entity, LastSystemVersion);
                        bool healthChanged = healthLookup.DidChange(targetEntity, LastSystemVersion);
                        bool resourceChanged = resourceLookup.HasComponent(targetEntity) && resourceLookup.DidChange(targetEntity, LastSystemVersion);

                        if (targetChanged || healthChanged || resourceChanged)
                        {
                            var targetHealth = healthLookup[targetEntity];
                            
                            foreach (var receiver in receivers)
                            {
                                // Интерфейс TargetFrame должен реализовывать этот кастомный ресивер
                                if (receiver is IEcsUiTargetReceiver targetUi)
                                {
                                    // Передаем данные компоненты цели напрямую в UI
                                    var targetResource = resourceLookup.HasComponent(targetEntity) 
                                        ? resourceLookup[targetEntity] 
                                        : default;

                                    targetUi.UpdateTargetInfo(ref targetHealth, ref targetResource);
                                }
                            }
                        }
                    }
                    else if (combatLookup.DidChange(entity, LastSystemVersion))
                    {
                        // Если таргет сбросился (стал Null) в этом кадре — очищаем UI
                        foreach (var receiver in receivers)
                        {
                            if (receiver is IEcsUiTargetReceiver targetUi)
                            {
                                targetUi.ClearTarget();
                            }
                        }
                    }
                }
            }
        }
    }
}


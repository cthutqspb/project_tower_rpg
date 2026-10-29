using Unity.Entities;
using UnityEngine.UIElements;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UIPullSystem : SystemBase
    {
        // 🚪 Ресивер «жив» только пока он в панели. Закрытые/уничтоженные окна не кормим.
        private static bool IsAlive(object receiver)
        {
            return receiver is not VisualElement ve || ve.panel != null;
        }

        protected override void OnUpdate()
        {
            // 🔍 1. БЫСТРЫЕ, ПОТОКОБЕЗОПАСНЫЕ ЛУКАПЫ КОМПОНЕНТОВ (Чтение)
            var slotLookup = SystemAPI.GetBufferLookup<ItemSlot>(true);
            var actionBarLookup = SystemAPI.GetBufferLookup<ActionBarSlot>(true);
            var auraLookup = SystemAPI.GetBufferLookup<AuraSlot>(true);
            var healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
            var resourceLookup = SystemAPI.GetComponentLookup<ResourceComponent>(true);
            var unitLookup = SystemAPI.GetComponentLookup<UnitComponent>(true);

            // ⚠️ combatLookup нигде не используется, я его убрал, чтобы не тратить время
            // var combatLookup = SystemAPI.GetComponentLookup<CombatStateComponent>(true);

            var unitCurrentAttributesLookup = SystemAPI.GetComponentLookup<UnitCurrentAttributesComponent>(true);
            var unitCombatStatsLookup = SystemAPI.GetComponentLookup<UnitCombatStatsComponent>(true);

            var castLookup = SystemAPI.GetComponentLookup<CastComponent>(true);

            var questLookup = SystemAPI.GetBufferLookup<QuestComponent>(true);
            var questObjectiveLookup = SystemAPI.GetBufferLookup<QuestObjectiveComponent>(true);
            var dialogueStateLookup = SystemAPI.GetComponentLookup<DialogueStateComponent>(true);
            var dialogueOptionsLookup = SystemAPI.GetBufferLookup<DialogueOptionElement>(true);

            // 🔄 2. ЕДИНЫЙ ЦИКЛ ПО ВСЕМУ АКТИВНОМУ ИНТЕРФЕЙСУ ИГРЫ
            foreach (var entity in UIRegistry.GetActiveEntities())
            {
                var receivers = UIRegistry.GetReceivers(entity);
                if (receivers == null || receivers.Count == 0) continue;

                // ================================================================
                // СЛАЙС А: Инвентарь / Кукла персонажа
                // ================================================================
                if (slotLookup.HasBuffer(entity) && slotLookup.DidChange(entity, LastSystemVersion))
                {
                    var slots = slotLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<ItemSlot> ui && IsAlive(receiver))
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
                        if (receiver is IEcsUiBufferReceiver<ActionBarSlot> barUi && IsAlive(receiver))
                        {
                            barUi.UpdateFromBuffer(barSlots, false);
                        }
                    }
                }

                // =========================================================================
                // 🔮 РEАКТИВНЫЙ АУРА-МОСТ
                // =========================================================================
                if (auraLookup.HasBuffer(entity) && auraLookup.DidChange(entity, LastSystemVersion))
                {
                    var auraSlots = auraLookup[entity];
                    
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<AuraSlot> auraUi && IsAlive(receiver))
                        {
                            auraUi.UpdateFromBuffer(auraSlots, false);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Б: ЗДОРОВЬЕ ЮНИТА (UnitFrame)
                // ================================================================
                if (healthLookup.HasComponent(entity) && healthLookup.DidChange(entity, LastSystemVersion))
                {
                    var healthData = healthLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<HealthComponent> healthUi && IsAlive(receiver))
                        {
                            healthUi.UpdateFromComponent(ref healthData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС В: РЕСУРС ЮНИТА (UnitFrame)
                // ================================================================
                if (resourceLookup.HasComponent(entity) && resourceLookup.DidChange(entity, LastSystemVersion))
                {   
                    var resourceData = resourceLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<ResourceComponent> resourceUi && IsAlive(receiver))
                        {   
                            resourceUi.UpdateFromComponent(ref resourceData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Г: ЮНИТ (UnitFrame)
                // ================================================================
                if (unitLookup.HasComponent(entity) && unitLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitData = unitLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitComponent> unitUi && IsAlive(receiver))
                        {   
                            unitUi.UpdateFromComponent(ref unitData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Д: ТЕКУЩИЕ АТРИБУТЫ ЮНИТА (UnitStats)
                // ================================================================
                if (unitCurrentAttributesLookup.HasComponent(entity) && unitCurrentAttributesLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitCurrentAttributesData = unitCurrentAttributesLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitCurrentAttributesComponent> unitStatsUi && IsAlive(receiver))
                        {   
                            unitStatsUi.UpdateFromComponent(ref unitCurrentAttributesData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС E: СТАТЫ ЮНИТА (UnitStats)
                // ================================================================
                if (unitCombatStatsLookup.HasComponent(entity) && unitCombatStatsLookup.DidChange(entity, LastSystemVersion))
                {   
                    var unitCombatStatsData = unitCombatStatsLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<UnitCombatStatsComponent> unitStatsUi && IsAlive(receiver))
                        {   
                            unitStatsUi.UpdateFromComponent(ref unitCombatStatsData);
                        }
                    }
                }

                // ================================================================
                // 🌟 СЛАЙС Ж: КАСТ ЮНИТА
                // ================================================================
                if (castLookup.HasComponent(entity))
                {
                    var castData = castLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<CastComponent> castUi && IsAlive(receiver))
                        {
                            castUi.UpdateFromComponent(ref castData);
                        }
                    }
                }

                // =========================================================================
                // 📊 РЕАКТИВНЫЙ КВЕСТOВЫЙ МОСТ
                // =========================================================================
                if (questLookup.HasBuffer(entity) && questLookup.DidChange(entity, LastSystemVersion))
                {
                    var questSlots = questLookup[entity];

                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<QuestComponent> questUi && IsAlive(receiver))
                        {
                            questUi.UpdateFromBuffer(questSlots, false);
                        }
                    }
                }

                if (questObjectiveLookup.HasBuffer(entity) && questObjectiveLookup.DidChange(entity, LastSystemVersion))
                {
                    var objectiveSlots = questObjectiveLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<QuestObjectiveComponent> objUi && IsAlive(receiver))
                        {
                            objUi.UpdateFromBuffer(objectiveSlots, false);
                        }
                    }
                }

                // Диалоговое состояние (текущий спикер, нода)
                if (dialogueStateLookup.HasComponent(entity) && dialogueStateLookup.DidChange(entity, LastSystemVersion))
                {
                    var stateData = dialogueStateLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiComponentReceiver<DialogueStateComponent> stateUi && IsAlive(receiver))
                        {
                            stateUi.UpdateFromComponent(ref stateData);
                        }
                    }
                }

                // Буфер видимых опций диалога
                if (dialogueOptionsLookup.HasBuffer(entity) && dialogueOptionsLookup.DidChange(entity, LastSystemVersion))
                {
                    var options = dialogueOptionsLookup[entity];
                    foreach (var receiver in receivers)
                    {
                        if (receiver is IEcsUiBufferReceiver<DialogueOptionElement> optUi && IsAlive(receiver))
                        {
                            optUi.UpdateFromBuffer(options, false);
                        }
                    }
                }
            }
        }
    }
}

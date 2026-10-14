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
            var auraLookup = SystemAPI.GetBufferLookup<AuraSlot>(true);
            var healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
            var resourceLookup = SystemAPI.GetComponentLookup<ResourceComponent>(true);
            var unitLookup = SystemAPI.GetComponentLookup<UnitComponent>(true);

            var combatLookup = SystemAPI.GetComponentLookup<CombatStateComponent>(true);

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

                // =========================================================================
                // 🔮 РEАКТИВНЫЙ АУРА-МОСТ: Слепой пулл активных баффов/дебаффов из ОЗУ чанка
                // =========================================================================
                if (auraLookup.HasBuffer(entity) && auraLookup.DidChange(entity, LastSystemVersion))
                {
                    var auraSlots = auraLookup[entity];
                    
                    foreach (var receiver in receivers)
                    {
                        // Намертво и вслепую скармливаем буфер аур любой сетке или окну на экране!
                        if (receiver is IEcsUiBufferReceiver<AuraSlot> auraUi)
                        {
                            auraUi.UpdateFromBuffer(auraSlots, false);
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

                // =========================================================================
                // 📊 РЕАКТИВНЫЙ КВЕСТOВЫЙ МОСТ: Копейка в копейку по канону Аур и Инвентаря!
                // =========================================================================
                if (questLookup.HasBuffer(entity) && questLookup.DidChange(entity, LastSystemVersion))
                {
                    var questSlots = questLookup[entity];
                    
                    foreach (var receiver in receivers)
                    {
                        // Вслепую скармливаем unmanaged-буфер квестов любому подписанному окну или оверлею!
                        if (receiver is IEcsUiBufferReceiver<QuestComponent> questUi)
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
                        if (receiver is IEcsUiBufferReceiver<QuestObjectiveComponent> objUi)
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
                        if (receiver is IEcsUiComponentReceiver<DialogueStateComponent> stateUi)
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
                        if (receiver is IEcsUiBufferReceiver<DialogueOptionElement> optUi)
                        {
                            optUi.UpdateFromBuffer(options, false);
                        }
                    }
                }
            }
        }
    }
}


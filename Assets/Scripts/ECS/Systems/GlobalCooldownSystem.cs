using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.Core.Abilities;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GlobalCooldownSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 🦾 ПРАВИЛЬНЫЙ DOTS-ПАТТЕРН: В Query передаем только валидные компоненты,
            // а доступ к Entity получаем через расширение .WithEntityAccess() в конце!
            foreach (var (combatRW, entity) in SystemAPI.Query<RefRW<CombatStateComponent>>().WithEntityAccess())
            {
                // =========================================================================
                // 1. ТИКАЕМ ГЛОБАЛЬНЫЙ КУЛДАУН (ГКД)
                // =========================================================================
                var combat = combatRW.ValueRW;

                if (combat.GcdRemaining > 0f)
                {
                    combat.GcdRemaining -= deltaTime;

                    if (combat.GcdRemaining <= 0f)
                    {
                        combat.GcdRemaining = 0f;
                        combat.GcdDuration = 0f; // Сбрасываем эталон, ГКД полностью остыло!
                    }

                    combatRW.ValueRW = combat; // Запекаем стейт обратно в чанк
                }

                // =========================================================================
                // 2. ТИКАЕМ ОБЫЧНЫЕ КУЛДАУНЫ СПОСОБНОСТЕЙ И ПРЕДМЕТОВ
                // =========================================================================
                if (SystemAPI.HasBuffer<ActiveCooldownElement>(entity))
                {
                    var cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(entity);

                    for (int i = cooldownsBuffer.Length - 1; i >= 0; i--)
                    {
                        var cooldown = cooldownsBuffer[i];
                        cooldown.Remaining -= deltaTime;

                        if (cooldown.Remaining <= 0f)
                        {
                            // 💥 МОМЕНТ ИСТИНЫ: Обычный кулдаун группы полностью остыл!
                            // Лезем в экшен-бар этого же юнита, чтобы найти все привязанные слоты
                            if (SystemAPI.HasBuffer<ActionBarSlot>(entity))
                            {
                                var barBuffer = SystemAPI.GetBuffer<ActionBarSlot>(entity);
                                
                                // Пробегаем по всем 24 кнопкам панели
                                for (int b = 0; b < barBuffer.Length; b++)
                                {
                                    var slotData = barBuffer[b];
                                    var abilityId = slotData.AbilityId.ToString();
                                    
                                    if (string.IsNullOrEmpty(abilityId)) continue;

                                    // Лезем в базу, чтобы узнать группу кулдауна этой кнопки
                                    var abilityCfg = AbilitiesDatabase.GetAbility(abilityId);
                                    if (abilityCfg != null && abilityCfg.parameters != null)
                                    {
                                        FixedString32Bytes slotCooldownGroup = abilityCfg.parameters.cooldown_group ?? abilityId;

                                        // Если группа кнопки совпадает с остывшей группой — триггерим вспышку по её точному индексу слота!
                                        if (slotCooldownGroup == cooldown.CooldownGroup)
                                        {
                                            UIEvents.TriggerSlotAnimate(slotData.SlotIndex, SlotAnimationType.CooldownReady); // Или просто передавай индекс цикла 'b'
                                        }
                                    }
                                }
                            }

                            // Кулдаун остыл и вспышки розданы — выкидываем его из памяти юнита
                            cooldownsBuffer.RemoveAt(i);
                        }
                        else
                        {
                            cooldownsBuffer[i] = cooldown;
                        }
                    }
                }
            }        
        }
    }
}


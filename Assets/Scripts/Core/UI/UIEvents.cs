using System;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public static class UIEvents
    {
        // Событие изменения инвентаря
        public static event Action<Entity, int> InventoryChanged;
        
        // Событие изменения здоровья
        public static event Action<Entity, int, int> HealthChanged;  // Entity, currentHP, maxHP
        
        // Событие изменения ресурса (мана/ярость)
        public static event Action<Entity, int, int> ResourceChanged;
        
        // Событие изменения аур
        public static event Action<Entity, int> AuraChanged;  // Entity, slotIndex
        
        // Событие смены цели
        public static event Action<Entity> TargetChanged;
        
        // ================================================================
        // ТРИГГЕРЫ
        // ================================================================
        
        public static void TriggerInventoryChanged(Entity containerEntity, int slotIndex)
        {
            InventoryChanged?.Invoke(containerEntity, slotIndex);
        }
        
        public static void TriggerHealthChanged(Entity unitEntity, int currentHP, int maxHP)
        {
            HealthChanged?.Invoke(unitEntity, currentHP, maxHP);
        }
        
        public static void TriggerResourceChanged(Entity unitEntity, int current, int max)
        {
            ResourceChanged?.Invoke(unitEntity, current, max);
        }
        
        public static void TriggerAuraChanged(Entity unitEntity, int slotIndex)
        {
            AuraChanged?.Invoke(unitEntity, slotIndex);
        }
        
        public static void TriggerTargetChanged(Entity targetEntity)
        {
            TargetChanged?.Invoke(targetEntity);
        }
    }
}

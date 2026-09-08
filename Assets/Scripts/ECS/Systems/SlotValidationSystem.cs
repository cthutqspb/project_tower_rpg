using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SlotValidationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 🦾 ММО-КАНОН: Ищем сущность, которой мы сейчас физически управляем в ОЗУ чанков!
            if (!SystemAPI.TryGetSingletonEntity<ControlledByPlayerTag>(out var controlledEntity)) return;

            // Забираем безопасный Read-Only Lookup, чтобы намертво заблокировать ложные триггеры статов
            var barLookup = SystemAPI.GetBufferLookup<ActionBarSlot>(true);
            if (!barLookup.HasBuffer(controlledEntity)) return;

            var barSlots = barLookup[controlledEntity];
            // Получаем все сетки экшенбара, привязанные к управляемому существу
            var receivers = UIRegistry.GetReceivers(controlledEntity);
            if (receivers == null) return;

            // ✅ 1. Получаем состояние ГКД
            var combatState = SystemAPI.GetComponent<CombatStateComponent>(controlledEntity);
            float gcdRemaining = combatState.GcdRemaining;
            float gcdDuration = combatState.GcdDuration;

            // ✅ 2. Получаем буфер ОБЫЧНЫХ КУЛДАУНОВ (для способностей и предметов)
            DynamicBuffer<ActiveCooldownElement> cooldownsBuffer = default;
            if (SystemAPI.HasBuffer<ActiveCooldownElement>(controlledEntity))
            {
                cooldownsBuffer = SystemAPI.GetBuffer<ActiveCooldownElement>(controlledEntity);
            }

            // ✅ 3. Получаем ссылки на сущности инвентаря и куклы для будущих TODO-веток
            Entity inventoryEntity = Entity.Null;
            Entity paperdollEntity = Entity.Null;

            if (SystemAPI.HasComponent<BuffersLinkComponent>(controlledEntity))
            {
                var links = SystemAPI.GetComponent<BuffersLinkComponent>(controlledEntity);
                inventoryEntity = links.Inventory;
                paperdollEntity = links.Paperdoll;
            }

            // 🔄 4. ПОКАДРОВЫЙ ПУШ СОСТОЯНИЙ ВО ВСЕ АКТИВНЫЕ ИНТЕРФЕЙСЫ
            foreach (var receiver in receivers)
            {
                // 🔮 РЕЛЬСЫ ЭКШЕН-БАРА: Передаем ГКД и обычные КД способностей/итемов
                if (receiver is IEcsUiBufferReceiver<ActionBarSlot> barUi)
                {
                    // Метод вызывается каждую миллисекунду кадра, отдавая ГКД и буфер КД в экшен-бар
                    barUi.UpdateFromBuffer(barSlots, true, gcdRemaining, gcdDuration, cooldownsBuffer);
                }

                // =========================================================================
                // 🎒 TODO: РЕЛЬСЫ ИНВЕНТАРЯ (Покадровый рендер "часиков" на зельях и свитках)
                // =========================================================================
                // if (inventoryEntity != Entity.Null && receiver is IEcsUiBufferReceiver<ItemSlot> inventoryUi)
                // {
                //     // Сюда встанет покадровая валидация и отрисовка КД предметов в рюкзаке
                // }

                // =========================================================================
                // 👤 TODO: РЕЛЬСЫ КУКЛЫ ПЕРСОНАЖА (Покадровый рендер КД надетых тринкетов/вещей)
                // =========================================================================
                // if (paperdollEntity != Entity.Null && receiver is IEcsUiBufferReceiver<ItemSlot> paperdollUi)
                // {
                //     // Сюда встанет покадровая валидация и отрисовка КД надетых предметов экипировки
                // }
            }
        }
    }
}


using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    public struct ActionCommand : IComponentData
    {
        public FixedString64Bytes Type;
        public Entity SourceEntity;
        public int SourceSlot;
        public Entity TargetEntity;
        public int TargetSlot;
        public FixedString64Bytes ItemId;
        public int Amount;
        public float3 Position;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class ActionDispatcherSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;
        private BufferLookup<SlotData> _slotDataLookup;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _slotDataLookup = GetBufferLookup<SlotData>(false);
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();
            _slotDataLookup.Update(ref CheckedStateRef);

            // 🎯 СТЕРИЛЬНЫЙ И БЫСТРЫЙ ДИСПЕТЧЕР ЛУТА (БЕЗ СПАГЕТТИ):
            foreach (var (item, intent, entity) in 
                     SystemAPI.Query<RefRO<ItemComponent>, RefRO<ClickIntent>>().WithEntityAccess())
            {
                Entity actorEntity = intent.ValueRO.Actor;
                Entity targetInventory = Entity.Null;

                // Получаем EntityManager
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;

                // Для ЛЮБОГО юнита (игрок, монстр, NPC) — ищем инвентарь по Owner + InventoryTag
                targetInventory = ContainerHelper.GetContainerForUnit<InventoryTag>(actorEntity, em);

                if (targetInventory == Entity.Null)
                {
                    Debug.LogError($"[ActionDispatcher] Инвентарь для актора {actorEntity.Index} не найден!");
                }
                if (targetInventory != Entity.Null)
                {
                    // Твой родной, кристально чистый вызов экшена лута без нарушения многопоточности!
                    ItemActions.Loot(
                        ref _slotDataLookup,
                        ecb,
                        entity, // Сущность шмотки на земле
                        targetInventory // Сущность рюкзака, куда летит предмет
                    );
                }
                else
                {
                    Debug.LogError($"[ActionDispatcher КРИТ]: Не удалось найти инвентарь в реестре для актера {actorEntity.Index}!");
                }

                ecb.RemoveComponent<ClickIntent>(entity);
                Debug.Log($"[ActionDispatcher] Клик по ПРЕДМЕТУ {entity.Index} от Актера {actorEntity.Index} направлен напрямую в ItemActions.Loot.");
            }

            foreach (var (cmd, entity) in 
                     SystemAPI.Query<RefRO<ActionCommand>>().WithEntityAccess())
            {
                var type = cmd.ValueRO.Type.ToString();

                switch (type)
                {
                    case "item_transfer":
                        ExecuteItemTransfer(cmd.ValueRO);
                        break;
                    
                    case "item_drop":
                        ExecuteItemDrop(cmd.ValueRO, ecb);
                        break;

                    case "item_use":
                        ExecuteItemUse(cmd.ValueRO);
                        break;
                    
                    default:
                        Debug.LogWarning($"[ActionDispatcher]: Неизвестный тип {type}");
                        break;
                }

                ecb.DestroyEntity(entity);
            }

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        // ================================================================
        // 🎯 ИСПОЛНИТЕЛИ КОМАНД
        // ================================================================

        private void ExecuteItemTransfer(ActionCommand cmd)
        {
            Debug.Log($"[ActionDispatcher DEBUG]: SourceEntity Index = {cmd.SourceEntity.Index}, TargetEntity Index = {cmd.TargetEntity.Index}");
            ISlotContainer source = CreateContainer(cmd.SourceEntity);
            ISlotContainer target = CreateContainer(cmd.TargetEntity);
            
            if (source == null || target == null)
            {
                Debug.LogWarning("[ActionDispatcher] Не удалось создать контейнер для трансфера");
                return;
            }

            int finalTargetSlot = cmd.TargetSlot;

            // 🎯 Если TargetSlot == -1, значит это ДАБЛКЛИК (быстрый перенос), ищем слот автоматически
            if (finalTargetSlot == -1)
            {
                var targetSlotsBuffer = _slotDataLookup[cmd.TargetEntity];
                
                // Создаём фейковую структуру данных нашего входящего предмета для проверки правил CanPlaceContent
                var incomingContent = new SlotData 
                { 
                    DataId = cmd.ItemId, 
                    DataType = "item",
                    Amount = cmd.Amount
                };

                // Перебираем все слоты целевого контейнера (куклы или инвентаря)
                for (int i = 0; i < targetSlotsBuffer.Length; i++)
                {
                    // 1. Проверяем, подходит ли предмет в этот слот по правилам контейнера (например, по типу EquipSlot на кукле)
                    if (target.CanPlaceContent(i, incomingContent))
                    {
                        // 2. Дополнительно проверяем, что слот сейчас пустой (чтобы не перезаписать надетую вещь)
                        if (targetSlotsBuffer[i].IsEmpty)
                        {
                            finalTargetSlot = i;
                            break;
                        }
                    }
                }

                // Если подходящего пустого слота не нашлось (например, сумка полна или на кукле уже занят нужный слот)
                if (finalTargetSlot == -1)
                {
                    Debug.LogWarning($"[ActionDispatcher] Нет свободного или подходящего слота в контейнере для {cmd.ItemId}");
                    return;
                }
            }
            
            // Выполняем наш стандартный, проверенный трансфер!
            ItemActions.Transfer(source, cmd.SourceSlot, target, finalTargetSlot);
        }


        private ISlotContainer CreateContainer(Entity entity)
        {
            // Идеальный полиморфизм: все окна теперь работают через один BufferSlotContainer
            if (_slotDataLookup.HasBuffer(entity))
            {
                return new BufferSlotContainer(entity, _slotDataLookup);
            }
            
            return null;
        }

        private void ExecuteItemDrop(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ItemActions.Drop(
                ref _slotDataLookup,
                ecb,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.ItemId.ToString(),
                cmd.Amount,
                cmd.Position
            );
        }

        private void ExecuteItemUse(ActionCommand cmd)
        {
            // Из инвентаря/слота предмет использует (кастует) сущность-владелец SourceEntity
            ItemActions.Use(
                ref _slotDataLookup,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.ItemId.ToString(),
                cmd.SourceEntity // Передаем кастера (кто нажал на предмет)
            );
        }
    }
}


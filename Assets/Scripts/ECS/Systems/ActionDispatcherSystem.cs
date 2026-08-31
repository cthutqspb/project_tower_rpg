using UnityEngine;
using Unity.Entities;

using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.ECS.Reducers;
using ProjectTowerRpg.Core.UI;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class ActionDispatcherSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;
        private BufferLookup<ItemSlot> _slotDataLookup;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _slotDataLookup = GetBufferLookup<ItemSlot>(false);
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();
            _slotDataLookup.Update(ref CheckedStateRef);

            // ================================================================
            // ИСПОЛНЕНИЕ КОМАНД (Сверхзвуковой unmanaged switch по байту)
            // ================================================================
            foreach (var (cmd, entity) in 
                     SystemAPI.Query<RefRO<ActionCommand>>().WithEntityAccess())
            {
                // 🦾 ПРОЦЕССОРНЫЙ ДЗЕН: Никаких ToString() и скрытых аллокаций!
                // Прямой switch по твоему новому полю Action, содержащему ActionKind!
                switch (cmd.ValueRO.Action)
                {
                    case ActionKind.Loot:
                        ExecuteLoot(cmd.ValueRO, ecb);
                        break;

                    case ActionKind.OpenContainer:
                        ExecuteOpenContainer(cmd.ValueRO, ecb);
                        break;

                    case ActionKind.ContainerTakeAll:
                        ExecuteContainerTakeAll(cmd.ValueRO);
                        break;

                    case ActionKind.Attack:
                        ExecuteAttack(cmd.ValueRO);
                        break;

                    case ActionKind.Interact:
                        ExecuteInteract(cmd.ValueRO);
                        break;

                    case ActionKind.MoveTo:
                        ExecuteMoveTo(cmd.ValueRO);
                        break;

                    case ActionKind.ItemTransfer:
                        ExecuteItemTransfer(cmd.ValueRO);
                        break;

                    case ActionKind.ItemDrop:
                        ExecuteItemDrop(cmd.ValueRO, ecb);
                        break;

                    case ActionKind.ItemUse:
                        ExecuteItemUse(cmd.ValueRO);
                        break;

                    case ActionKind.ActionBarAssign:
                        ExecuteActionBarAssign(cmd.ValueRO);
                        break;

                    default:
                        Debug.LogWarning($"[ActionDispatcher]: Неизвестный экшен '{cmd.ValueRO.Action}'");
                        break;
                }

                ecb.DestroyEntity(entity);
            }

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        // ================================================================
        // ИСПОЛНИТЕЛИ
        // ================================================================

        private void ExecuteLoot(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var targetInventory = ContainerHelper.GetContainerForUnit<InventoryTag>(cmd.SourceEntity, em);

            if (targetInventory == Entity.Null)
            {
                Debug.LogError($"[ActionDispatcher] Инвентарь для актора {cmd.SourceEntity.Index} не найден!");
                return;
            }

            ItemReducer.Loot(ref _slotDataLookup, ecb, cmd.TargetEntity, targetInventory);
            Debug.Log($"[ActionDispatcher] Лут {cmd.TargetEntity.Index} -> {targetInventory.Index}");
        }

        private void ExecuteOpenContainer(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ContainerReducer.Open(cmd.TargetEntity, ecb);
            Debug.Log($"[ActionDispatcher] Открыт контейнер {cmd.TargetEntity.Index}");
        }

        private void ExecuteContainerTakeAll(ActionCommand cmd)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 1. Узнаем Entity рантайм-мешка сундука, который мы грабим
            Entity sourceBagEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(cmd.TargetEntity, entityManager);
            
            // 2. Узнаем Entity рантайм-мешка рюкзака игрока, куда переливаем вещи
            Entity targetBagEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(cmd.SourceEntity, entityManager);

            if (sourceBagEntity == Entity.Null || targetBagEntity == Entity.Null)
            {
                Debug.LogWarning("[ActionDispatcher] 'ВЗЯТЬ ВСЁ' отменено: не найден мешок сундука или игрока.");
                return;
            }

            // 3. Получаем доступ к буферу слотов сундука, чтобы узнать, сколько там ячеек
            var sourceSlotsBuffer = _slotDataLookup[sourceBagEntity];

            // 🔄 Бежим по ячейкам мешка сундука
            for (int i = 0; i < sourceSlotsBuffer.Length; i++)
            {
                var slotData = sourceSlotsBuffer[i];

                // 💰 Если в слоте сундука есть реальный предмет — шёлково скармливаем команду твоему трансферу!
                if (!slotData.IsEmpty)
                {
                    // Создаем промежуточную подкоманду на перенос конкретного слота
                    var singleTransferCommand = new ActionCommand
                    {
                        Action = ItemActions.Transfer,
                        SourceEntity = sourceBagEntity, // Скормили Entity самого мешка сундука!
                        SourceSlot = i,
                        TargetEntity = targetBagEntity, // Скормили Entity самого рюкзака игрока!
                        TargetSlot = -1,                // Диспетчер сам найдет пустой слот по правилам CanPlaceContent
                        ItemId = slotData.DataId,
                        Amount = slotData.Amount
                    };

                    // Нагло вызываем твой готовый метод! Он сам создаст BufferSlotContainer и сделает Transfer!
                    ExecuteItemTransfer(singleTransferCommand);
                }
            }

            Debug.Log($"💰 [ActionDispatcher]: Экшен 'ВЗЯТЬ ВСЁ' шёлково перелил предметы из мешка {sourceBagEntity.Index} в рюкзак {targetBagEntity.Index}.");
        }


        private void ExecuteAttack(ActionCommand cmd)
        {
            Debug.Log($"[ActionDispatcher] Атака {cmd.SourceEntity.Index} -> {cmd.TargetEntity.Index}");
            // UnitActions.Attack(cmd.SourceEntity, cmd.TargetEntity);
        }

        private void ExecuteInteract(ActionCommand cmd)
        {
            Debug.Log($"[ActionDispatcher] Интеракт {cmd.SourceEntity.Index} -> {cmd.TargetEntity.Index}");
            // UnitActions.Interact(cmd.SourceEntity, cmd.TargetEntity);
        }

        private void ExecuteMoveTo(ActionCommand cmd)
        {
            Debug.Log($"[ActionDispatcher] Движение {cmd.SourceEntity.Index} -> {cmd.Position}");
            // MovementActions.MoveTo(cmd.SourceEntity, cmd.Position);
        }

        private void ExecuteItemTransfer(ActionCommand cmd)
        {
            ISlotContainer source = CreateContainer(cmd.SourceEntity);
            ISlotContainer target = CreateContainer(cmd.TargetEntity);
            
            if (source == null || target == null)
            {
                Debug.LogWarning("[ActionDispatcher] Не удалось создать контейнер для трансфера");
                return;
            }

            int finalTargetSlot = cmd.TargetSlot;

            if (finalTargetSlot == -1)
            {
                var targetSlotsBuffer = _slotDataLookup[cmd.TargetEntity];
                var incomingContent = new ItemSlot
                {
                    DataId = cmd.ItemId,
                    DataType = "item",
                    Amount = cmd.Amount
                };

                // 🦾 ИСПРАВЛЕНО: Этот цикл ищет анатомический слот ТОЛЬКО если цель — кукла персонажа!
                // Проверяем по ECS-тегу целевой сущности
                if (World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<PaperdollTag>(cmd.TargetEntity))
                {
                    for (int i = 0; i < targetSlotsBuffer.Length; i++)
                    {
                        if (target.CanPlaceContent(i, incomingContent))
                        {
                            finalTargetSlot = i;
                            break;
                        }
                    }
                }

                // 2. ЕСЛИ НЕ НАШЛИ (ИЛИ ЭТО ИНВЕНТАРЬ) — ИЩЕМ ПУСТОЙ СЛОТ
                if (finalTargetSlot == -1)
                {
                    for (int i = 0; i < targetSlotsBuffer.Length; i++)
                    {
                        if (targetSlotsBuffer[i].IsEmpty)
                        {
                            finalTargetSlot = i;
                            break;
                        }
                    }
                }

                if (finalTargetSlot == -1)
                {
                    Debug.LogWarning($"[ActionDispatcher] Нет подходящего слота для {cmd.ItemId}");
                    return;
                }
            }

            ItemReducer.Transfer(source, cmd.SourceSlot, target, finalTargetSlot);
            Debug.Log($"[ActionDispatcher] Трансфер {cmd.ItemId} [{cmd.SourceSlot}] -> [{finalTargetSlot}]");
        }

        private void ExecuteItemDrop(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ItemReducer.Drop(
                ref _slotDataLookup,
                ecb,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.Position
            );

            Debug.Log($"[ActionDispatcher] Дроп {cmd.ItemId} x{cmd.Amount} на {cmd.Position}");
        }

        private void ExecuteItemUse(ActionCommand cmd)
        {
            ItemReducer.Use(
                ref _slotDataLookup,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.ItemId.ToString(),
                cmd.SourceEntity
            );

            Debug.Log($"[ActionDispatcher] Использован {cmd.ItemId} актором {cmd.SourceEntity.Index}");
        }

        private void ExecuteActionBarAssign(ActionCommand cmd)
        {
            PlayerReducer.ActionBarAssign(EntityManager, cmd);
        }

        private ISlotContainer CreateContainer(Entity entity)
        {
            return _slotDataLookup.HasBuffer(entity)
                ? new BufferSlotContainer(entity, _slotDataLookup)
                : null;
        }
    }
}

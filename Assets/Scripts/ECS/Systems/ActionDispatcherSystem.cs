using Unity.Entities;
using Unity.Collections;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.ECS.Reducers;
using Debug = UnityEngine.Debug;

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

            // 🦾 ОДИН СИНГЛТОН НА ВЕСЬ КАДР:
            // Ищем центральный кабель шины ивентов ровно один раз на входе в систему!
            if (!SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var presentationBufferEntity))
            {
                Debug.LogError("[ActionDispatcher]: Критическая ошибка: Не найдена глобальная шина PresentationEvent!");
                return;
            }

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
                        ExecuteLoot(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;

                    case ActionKind.OpenContainer:
                        ExecuteOpenContainer(cmd.ValueRO, ecb);
                        break;

                    case ActionKind.ContainerTakeAll:
                        ExecuteContainerTakeAll(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;
                    

                    case ActionKind.Interact:
                        ExecuteInteract(cmd.ValueRO, ecb);
                        break;
                    
                    case ActionKind.ItemTransfer:
                        ExecuteItemTransfer(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;

                    case ActionKind.ItemDrop:
                        ExecuteItemDrop(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;

                    case ActionKind.ItemUse:
                        ExecuteItemUse(cmd.ValueRO, ecb);
                        break;

                    case ActionKind.ItemCreate:
                        ExecuteItemCreate(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;

                    case ActionKind.ItemSplit:
                        ExecuteItemSplit(cmd.ValueRO, ecb, presentationBufferEntity);
                        break;

                    case ActionKind.ActionBarAssign:
                        ExecuteActionBarAssign(cmd.ValueRO);
                        break;

                    case ActionKind.AuraDisable:
                        ExecuteAuraDisable(cmd.ValueRO);
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

        private void ExecuteLoot(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!em.HasComponent<BuffersLinkComponent>(cmd.SourceEntity))
            {
                Debug.LogError($"[ActionDispatcher] У актора {cmd.SourceEntity.Index} отсутствует компонент связей буферов!");
                return;
            }

            var links = em.GetComponentData<BuffersLinkComponent>(cmd.SourceEntity);
            Entity targetInventory = links.Inventory;

            if (targetInventory == Entity.Null || !em.Exists(targetInventory))
            {
                Debug.LogError($"[ActionDispatcher] Инвентарь для актора {cmd.SourceEntity.Index} пуст или уничтожен в ОЗУ!");
                return;
            }

            ItemReducer.Loot(ref _slotDataLookup, ecb, cmd.TargetEntity, targetInventory, presentationBufferEntity);
            Debug.Log($"[ActionDispatcher] Лут {cmd.TargetEntity.Index} -> {targetInventory.Index}");
        }

        private void ExecuteOpenContainer(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ContainerReducer.Open(cmd.TargetEntity, ecb);
            Debug.Log($"[ActionDispatcher] Открыт контейнер {cmd.TargetEntity.Index}");
        }

        private void ExecuteContainerTakeAll(
            ActionCommand cmd,
            EntityCommandBuffer ecb,
            Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!em.HasComponent<BuffersLinkComponent>(cmd.TargetEntity))
            {
                Debug.LogError($"[ActionDispatcher] У сундука {cmd.TargetEntity.Index} отсутствует паспорт связей буферов!");
                return;
            }

            Entity sourceBagEntity = em.GetComponentData<BuffersLinkComponent>(cmd.TargetEntity).Inventory;
            
            if (!em.HasComponent<BuffersLinkComponent>(cmd.SourceEntity))
            {
                Debug.LogError($"[ActionDispatcher] У актора {cmd.SourceEntity.Index} отсутствует паспорт связей буферов!");
                return;
            }

            Entity targetBagEntity = em.GetComponentData<BuffersLinkComponent>(cmd.SourceEntity).Inventory;

            if (sourceBagEntity == Entity.Null || targetBagEntity == Entity.Null || !em.Exists(sourceBagEntity) || !em.Exists(targetBagEntity))
            {
                Debug.LogWarning("[ActionDispatcher] 'ВЗЯТЬ ВСЁ' отменено: не найден легитимный мешок сундука или рюкзак игрока в ОЗУ.");
                return;
            }

            // 🎯 Вся логика поиска слотов, стаков и переноса теперь внутри редюсера
            ItemReducer.TakeAll(ref _slotDataLookup, ecb, sourceBagEntity, targetBagEntity, presentationBufferEntity);

            Debug.Log($"💰 [ActionDispatcher]: Экшен 'ВЗЯТЬ ВСЁ' перелил предметы из мешка {sourceBagEntity.Index} в рюкзак {targetBagEntity.Index}.");
        }

        private void ExecuteInteract(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // 🦾 СВЕРХЗВУКОВАЯ СИММЕТРИЯ: Вызываем редюсер без лишнего мусора!
            PlayerReducer.Interact(em, ecb, cmd.SourceEntity, cmd.TargetEntity);
        }
        
        private void ExecuteItemTransfer(
            ActionCommand cmd,
            EntityCommandBuffer ecb,
            Entity presentationBufferEntity)
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
                    ContentType = SlotContentType.Item,
                    Amount = cmd.Amount
                };

                // 1. Если цель — кукла, ищем анатомический слот
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

                // 🎯 2. Если не нашли — ищем слот с ТАКИМ ЖЕ предметом (для стака)
                if (finalTargetSlot == -1)
                {
                    for (int i = 0; i < targetSlotsBuffer.Length; i++)
                    {
                        if (!targetSlotsBuffer[i].IsEmpty && targetSlotsBuffer[i].DataId == cmd.ItemId)
                        {
                            finalTargetSlot = i;
                            break;
                        }
                    }
                }

                // 🎯 3. Если стак не нашли — ищем пустой слот
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
            Debug.Log($"[ActionDispatcher] TRY TRANSFER: {cmd.ItemId} from {cmd.SourceEntity.Index} slot {cmd.SourceSlot} to {cmd.TargetEntity.Index} slot {finalTargetSlot}");
            // Передаем найденный слот в редюсер.
            // Если это был стак, но он переполнен — редюсер сам сделает своп или откажет.
            ItemReducer.Transfer(
                ecb,
                source,
                cmd.SourceSlot,
                target,
                finalTargetSlot,
                presentationBufferEntity
            );
        }

        private void ExecuteItemDrop(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            ItemReducer.Drop(
                ref _slotDataLookup,
                ecb,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.Position,
                presentationBufferEntity
            );

            Debug.Log($"[ActionDispatcher] Дроп {cmd.ItemId} x{cmd.Amount} на {cmd.Position}");
        }

        private void ExecuteItemUse(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ItemReducer.Use(
                ecb,
                ref _slotDataLookup,
                cmd.SourceEntity,
                cmd.SourceSlot,
                cmd.ItemId.ToString(),
                cmd.SourceEntity
            );

            Debug.Log($"[ActionDispatcher] Использован {cmd.ItemId} актором {cmd.SourceEntity.Index}");
        }

        private void ExecuteItemSplit(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            Debug.Log($"[ActionDispatcher] Split: {cmd.ItemId} x{cmd.Amount} из слота #{cmd.SourceSlot} → " +
                      $"{(cmd.TargetEntity == Entity.Null ? "земля" : $"слот #{cmd.TargetSlot}")}");

            ItemReducer.Split(
                ref _slotDataLookup,
                ecb,
                cmd.SourceEntity,   // контейнер-источник
                cmd.SourceSlot,     // откуда отрезаем
                cmd.Amount,         // сколько отрезать (из DragData.Amount)
                cmd.TargetEntity,   // куда: Entity.Null = мир, иначе контейнер
                cmd.TargetSlot,     // -1 = мир
                cmd.Position,       // позиция дропа (используется только если TargetEntity == Null)
                presentationBufferEntity
            );
        }

        private void ExecuteItemCreate(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!em.HasComponent<BuffersLinkComponent>(cmd.TargetEntity))
            {
                Debug.LogError($"[ActionDispatcher.ItemCreate] У {cmd.TargetEntity.Index} нет BuffersLinkComponent");
                return;
            }

            var links = em.GetComponentData<BuffersLinkComponent>(cmd.TargetEntity);
            Entity inventory = links.Inventory;

            if (inventory == Entity.Null || !em.Exists(inventory))
            {
                Debug.LogError($"[ActionDispatcher.ItemCreate] Инвентарь не найден");
                return;
            }

            ItemReducer.Create(
                ecb,
                ref _slotDataLookup,
                inventory,
                cmd.ItemId.ToString(),
                cmd.Amount,
                presentationBufferEntity
            );
        }

        private void ExecuteActionBarAssign(ActionCommand cmd)
        {
            PlayerReducer.ActionBarAssign(EntityManager, cmd);
        }

        private void ExecuteAuraDisable(ActionCommand cmd)
        {
            PlayerReducer.AuraDisable(EntityManager, cmd);
        }

        private ISlotContainer CreateContainer(Entity entity)
        {
            return _slotDataLookup.HasBuffer(entity)
                ? new BufferSlotContainer(entity, _slotDataLookup)
                : null;
        }
    }
}

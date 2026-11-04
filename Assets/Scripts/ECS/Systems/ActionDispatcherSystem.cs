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

            if (!SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var presentationBufferEntity))
            {
                Debug.LogError("[ActionDispatcher]: Критическая ошибка: Не найдена глобальная шина PresentationEvent!");
                return;
            }

            foreach (var (cmd, entity) in 
                     SystemAPI.Query<RefRO<ActionCommand>>().WithEntityAccess())
            {
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
                    case ActionKind.Converse:
                        ExecuteConverse(cmd.ValueRO, ecb);
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
        // ХЕЛПЕР: ПОИСК СЛОТА (кукла → стак → пустой)
        // ================================================================
        private int FindTargetSlot(ISlotContainer target, Entity targetInventory, int targetSlot, ItemSlot incomingContent)
        {
            if (targetSlot != -1) return targetSlot;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var targetSlotsBuffer = _slotDataLookup[targetInventory];

            // 1. Кукла — анатомический слот
            if (em.HasComponent<PaperdollTag>(targetInventory))
            {
                for (int i = 0; i < targetSlotsBuffer.Length; i++)
                {
                    if (target.CanPlaceContent(i, incomingContent))
                        return i;
                }
            }

            // 2. Слот с таким же предметом (стак)
            for (int i = 0; i < targetSlotsBuffer.Length; i++)
            {
                if (!targetSlotsBuffer[i].IsEmpty && targetSlotsBuffer[i].DataId == incomingContent.DataId)
                    return i;
            }

            // 3. Пустой слот
            for (int i = 0; i < targetSlotsBuffer.Length; i++)
            {
                if (targetSlotsBuffer[i].IsEmpty)
                    return i;
            }

            return -1;
        }

        // ================================================================
        // ИСПОЛНИТЕЛИ
        // ================================================================
        private void ExecuteLoot(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<BuffersLinkComponent>(cmd.SourceEntity)) return;

            var links = em.GetComponentData<BuffersLinkComponent>(cmd.SourceEntity);
            Entity targetInventory = links.Inventory;
            if (targetInventory == Entity.Null || !em.Exists(targetInventory)) return;

            ItemReducer.Loot(ref _slotDataLookup, ecb, cmd.TargetEntity, targetInventory, presentationBufferEntity);
        }

        private void ExecuteOpenContainer(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ContainerReducer.Open(cmd.TargetEntity, ecb);
        }

        private void ExecuteContainerTakeAll(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<BuffersLinkComponent>(cmd.TargetEntity)) return;
            if (!em.HasComponent<BuffersLinkComponent>(cmd.SourceEntity)) return;

            Entity sourceBagEntity = em.GetComponentData<BuffersLinkComponent>(cmd.TargetEntity).Inventory;
            Entity targetBagEntity = em.GetComponentData<BuffersLinkComponent>(cmd.SourceEntity).Inventory;

            if (sourceBagEntity == Entity.Null || targetBagEntity == Entity.Null) return;

            ItemReducer.TakeAll(ref _slotDataLookup, ecb, sourceBagEntity, targetBagEntity, presentationBufferEntity);
        }

        private void ExecuteConverse(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            PlayerReducer.Converse(em, ecb, cmd.SourceEntity, cmd.TargetEntity);
        }       
        
        private void ExecuteItemTransfer(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            Entity sourceInventory = cmd.SourceEntity;
            if (em.HasComponent<BuffersLinkComponent>(sourceInventory))
                sourceInventory = em.GetComponentData<BuffersLinkComponent>(sourceInventory).Inventory;

            Entity targetInventory = cmd.TargetEntity;
            if (em.HasComponent<BuffersLinkComponent>(targetInventory))
                targetInventory = em.GetComponentData<BuffersLinkComponent>(targetInventory).Inventory;

            ISlotContainer source = CreateContainer(sourceInventory);
            ISlotContainer target = CreateContainer(targetInventory);

            if (source == null || target == null) return;

            var incomingContent = new ItemSlot
            {
                DataId = cmd.ItemId,
                ContentType = SlotContentType.Item,
                Amount = cmd.Amount
            };

            int finalTargetSlot = FindTargetSlot(target, targetInventory, cmd.TargetSlot, incomingContent);
            if (finalTargetSlot == -1)
            {
                Debug.LogWarning($"[ActionDispatcher] Нет подходящего слота для {cmd.ItemId}");
                return;
            }

            ItemReducer.Transfer(ecb, source, cmd.SourceSlot, target, finalTargetSlot, presentationBufferEntity);
        }

        private void ExecuteItemDrop(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            ItemReducer.Drop(ref _slotDataLookup, ecb, cmd.SourceEntity, cmd.SourceSlot, cmd.Position, presentationBufferEntity);
        }

        private void ExecuteItemUse(ActionCommand cmd, EntityCommandBuffer ecb)
        {
            ItemReducer.Use(ecb, ref _slotDataLookup, cmd.SourceEntity, cmd.SourceSlot, cmd.ItemId.ToString(), cmd.SourceEntity);
        }

        private void ExecuteItemSplit(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 🎯 Резолвим инвентарь цели (если цель не мир)
        Entity targetInventory = cmd.TargetEntity;
        if (cmd.TargetEntity != Entity.Null && em.HasComponent<BuffersLinkComponent>(targetInventory))
            targetInventory = em.GetComponentData<BuffersLinkComponent>(targetInventory).Inventory;

        ISlotContainer target = cmd.TargetEntity != Entity.Null ? CreateContainer(targetInventory) : null;

        var incomingContent = new ItemSlot
        {
            DataId = cmd.ItemId,
            ContentType = SlotContentType.Item,
            Amount = cmd.Amount
        };

        int targetSlot = cmd.TargetSlot;
        if (cmd.TargetEntity != Entity.Null)
        {
            targetSlot = FindTargetSlot(target, targetInventory, cmd.TargetSlot, incomingContent);
            if (targetSlot == -1)
            {
                Debug.LogWarning($"[ActionDispatcher] Нет места для сплита {cmd.ItemId} у цели");
                return;
            }
        }

        Debug.Log($"[Dispatcher] Split: item={cmd.ItemId}, amount={cmd.Amount}, target={targetInventory.Index}, targetSlot={targetSlot}");

        ItemReducer.Split(
            ref _slotDataLookup,
            ecb,
            cmd.SourceEntity,
            cmd.SourceSlot,
            cmd.Amount,
            targetInventory, // 🎯 ВОТ ТУТ БЫЛА ОШИБКА (было cmd.TargetEntity)
            targetSlot,
            cmd.Position,
            presentationBufferEntity
        );
    }

        private void ExecuteItemCreate(ActionCommand cmd, EntityCommandBuffer ecb, Entity presentationBufferEntity)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<BuffersLinkComponent>(cmd.TargetEntity)) return;

            var links = em.GetComponentData<BuffersLinkComponent>(cmd.TargetEntity);
            Entity inventory = links.Inventory;
            if (inventory == Entity.Null || !em.Exists(inventory)) return;

            ItemReducer.Create(ecb, ref _slotDataLookup, inventory, cmd.ItemId.ToString(), cmd.Amount, presentationBufferEntity);
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

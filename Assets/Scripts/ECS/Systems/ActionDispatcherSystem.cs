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

            foreach (var (item, entity) in 
                     SystemAPI.Query<RefRO<ItemComponent>>().WithAll<ClickIntent>().WithEntityAccess())
            {
                ItemActions.Loot(
                    ref _slotDataLookup,
                    ecb,
                    entity,
                    EntityRegistry.Get("unit_inventory")
                );

                ecb.RemoveComponent<ClickIntent>(entity);
                Debug.Log($"[ActionDispatcher] Клик по ПРЕДМЕТУ {entity.Index} направлен напрямую в ItemActions.Loot.");
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
            // Используем новый универсальный интерфейс ISlotContainer
            ISlotContainer source = CreateContainer(cmd.SourceEntity);
            ISlotContainer target = CreateContainer(cmd.TargetEntity);
            
            if (source == null || target == null)
            {
                Debug.LogWarning("[ActionDispatcher] Не удалось создать контейнер для трансфера");
                return;
            }
            
            ItemActions.Transfer(source, cmd.SourceSlot, target, cmd.TargetSlot);
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


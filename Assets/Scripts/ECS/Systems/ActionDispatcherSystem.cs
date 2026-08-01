using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;

namespace ProjectTowerRpg.ECS.Systems
{
    // ================================================================
    // КОМАНДА (Перенесена внутрь общего пространства имен систем)
    // ================================================================
    public struct ActionCommand : IComponentData
    {
        public Unity.Collections.FixedString64Bytes Type;  // "item_transfer", "item_equip", "item_drop"
        public Entity SourceEntity;
        public int SourceSlot;
        public Entity TargetEntity;
        public int TargetSlot;
        public Unity.Collections.FixedString64Bytes ItemId;
        public int Amount;
        public Unity.Mathematics.float3 Position;          // Для дропа
    }

    // ================================================================
    // ДИСПЕТЧЕР
    // ================================================================
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
            int commandCount = 0;

            _slotDataLookup.Update(ref CheckedStateRef);

            foreach (var (cmd, entity) in 
                     SystemAPI.Query<RefRO<ActionCommand>>().WithEntityAccess())
            {
                commandCount++;
                Debug.Log($"[ActionDispatcher] Получена команда #{commandCount}: Type={cmd.ValueRO.Type}, SourceSlot={cmd.ValueRO.SourceSlot}, TargetSlot={cmd.ValueRO.TargetSlot}");
                var type = cmd.ValueRO.Type.ToString();

                switch (type)
                {
                    case "item_transfer":
                        ItemActions.Transfer(
                            ref _slotDataLookup,
                            cmd.ValueRO.SourceEntity,
                            cmd.ValueRO.SourceSlot,
                            cmd.ValueRO.TargetEntity,
                            cmd.ValueRO.TargetSlot,
                            cmd.ValueRO.ItemId.ToString(),
                            cmd.ValueRO.Amount
                        );
                        break;
                    
                    case "item_drop":
                        ItemActions.Drop(
                            ref _slotDataLookup,
                            cmd.ValueRO.SourceEntity,
                            cmd.ValueRO.SourceSlot,
                            cmd.ValueRO.ItemId.ToString(),
                            cmd.ValueRO.Amount,
                            cmd.ValueRO.Position
                        );
                        break;

                    case "item_use":
                        ItemActions.Use(
                            ref _slotDataLookup,
                            cmd.ValueRO.SourceEntity,
                            cmd.ValueRO.SourceSlot,
                            cmd.ValueRO.ItemId.ToString(),
                            cmd.ValueRO.TargetEntity
                        );
                        break;
                    
                    default:
                        Debug.LogWarning($"[ActionDispatcher]: Неизвестный тип {type}");
                        break;
                }

                ecb.DestroyEntity(entity);
            }

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}


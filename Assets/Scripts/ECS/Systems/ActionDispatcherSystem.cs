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
        public FixedString64Bytes ItemId;  // ← СТРОКА
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

            // ================================================================
            // 🚀 ПРЯМОЙ РОУТЕР КЛИКОВ
            // ================================================================
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

            // ================================================================
            // 🔄 ЦИКЛ ОБРАБОТКИ АСИНХРОННЫХ КОМАНД
            // ================================================================
            int commandCount = 0;

            foreach (var (cmd, entity) in 
                     SystemAPI.Query<RefRO<ActionCommand>>().WithEntityAccess())
            {
                commandCount++;
                Debug.Log($"[ActionDispatcher] Получена UI-команда #{commandCount}: Type={cmd.ValueRO.Type}");
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
                            cmd.ValueRO.ItemId.ToString(),  // ← FixedString → string
                            cmd.ValueRO.Amount
                        );
                        break;
                    
                    case "item_drop":
                        ItemActions.Drop(
                            ref _slotDataLookup,
                            ecb,
                            cmd.ValueRO.SourceEntity,
                            cmd.ValueRO.SourceSlot,
                            cmd.ValueRO.ItemId.ToString(),  // ← FixedString → string
                            cmd.ValueRO.Amount,
                            cmd.ValueRO.Position
                        );
                        break;

                    case "item_use":
                        ItemActions.Use(
                            ref _slotDataLookup,
                            cmd.ValueRO.SourceEntity,
                            cmd.ValueRO.SourceSlot,
                            cmd.ValueRO.ItemId.ToString(),  // ← FixedString → string
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

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class ItemSpawnSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();

            foreach (var (request, requestEntity) in 
                     SystemAPI.Query<RefRO<DropItemRequest>>().WithEntityAccess())
            {
                // 1. Рождаем чистую ECS-сущность в памяти
                Entity itemEntity = ecb.CreateEntity();

                string itemIdStr = request.ValueRO.ItemId.ToString();
                float3 spawnPosition = request.ValueRO.Position;
                spawnPosition.y = PhysicsUtils.GetGroundHeight(spawnPosition);

                // 🌟 ЧИСТАЯ СИММЕТРИЯ С ЮНИТАМИ: Сначала генерируем строковый UID по координатам чанка
                string generatedUidStr = $"i_{(int)spawnPosition.x}_{(int)spawnPosition.z}";
                
                // Для ECS-компонента берем чистый интовый хэш-код от этой строки (как у тебя в Бэйкере!)
                int generatedUidHash = generatedUidStr.GetHashCode();

                // Накатываем компоненты в ОЗУ
                ecb.AddComponent(itemEntity, LocalTransform.FromPosition(spawnPosition));
                ecb.AddComponent(itemEntity, new ItemComponent
                {
                    Uid = generatedUidHash,
                    ItemId = itemIdStr,  
                    Amount = request.ValueRO.Amount,
                    LootTableId = request.ValueRO.LootTableId,
                    IsLooted = request.ValueRO.IsLooted,
                    RespawnTime = request.ValueRO.RespawnTime
                });

                // =========================================================================
                // 🏗️ СВЯЗЬ МИРОВ: Загружаем ОДИН универсальный префаб из папки Resources!
                // =========================================================================
                var universalPrefab = Resources.Load<GameObject>("Items/default_item");
                
                if (universalPrefab != null)
                {
                    var spawnedModel = Object.Instantiate(universalPrefab, spawnPosition, Quaternion.identity);
                    spawnedModel.name = $"{itemIdStr}_{generatedUidStr}";

                    // 🌟 ИДЕАЛЬНАЯ СИММЕТРИЯ: Находим чистый рантайм-паспорт ItemView и заполняем строки!
                    var view = spawnedModel.GetComponent<ItemView>();
                    if (view != null)
                    {
                        view.uid = generatedUidStr; // Сюда шёлково залетает строка (string = string)
                        view.itemId = itemIdStr;
                        view.IsLinked = false; 
                    }
                }
                else
                {
                    Debug.LogError("🚨 ФАБРИКА ПРЕДМЕТОВ: Не удалось найти универсальный ItemPrefab по пути Resources/Items/Iu.prefab!");
                }

                Debug.Log($"[ItemSpawnSystem] Универсальный префаб заспавнен для: {itemIdStr}, Uid={generatedUidStr}");

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}


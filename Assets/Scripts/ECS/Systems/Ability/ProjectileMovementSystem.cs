using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using Debug = UnityEngine.Debug;
using quaternion = Unity.Mathematics.quaternion;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSpawnSystem))] // Тикает строго после спавна!
    public partial class ProjectileMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton< EndSimulationEntityCommandBufferSystem.Singleton >().CreateCommandBuffer(World.Unmanaged);
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Находим синглтон-сущность нашего буфера презентационных событий для визуала взрывов
            Entity eventBufferSingleton = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity< PresentationEventBufferTag >(out var bufferEntity))
            {
                eventBufferSingleton = bufferEntity;
            }

            // Выгребаем из чанков памяти все летящие снаряды
            var projectileQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly< ProjectileTag >(),
                ComponentType.ReadWrite< ProjectileMovement >(),
                ComponentType.ReadWrite< LocalTransform >()
            );
            var projectileEntities = projectileQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < projectileEntities.Length; i++)
            {
                Entity projectileEntity = projectileEntities[i];
                var movement = em.GetComponentData< ProjectileMovement >(projectileEntity);
                var transformData = em.GetComponentData< LocalTransform >(projectileEntity);

                Entity target = movement.TargetEntity;

                // WOW-ГВАРД ЦЕЛИ: Если цель умерла или испарилась, пока снаряд летел — тушим пулю в ОЗУ сервера
                if (!em.Exists(target) || em.HasComponent< IsDeadTag >(target))
                {
                    ecb.DestroyEntity(projectileEntity);
                    continue;
                }

                // Вытаскиваем свежие покадровые координаты цели из чанка памяти
                var targetTransform = em.GetComponentData< LocalTransform >(target);
                float3 targetPosition = targetTransform.Position;
                targetPosition.y += 1.2f; // Наводимся в грудь/центр модели, а не в ноги!

                float3 currentPosition = transformData.Position;

                // Рассчитываем вектор до цели и расстояние
                float3 vectorToTarget = targetPosition - currentPosition;
                float distanceToTarget = math.length(vectorToTarget);

                // =========================================================================
                // 💥 ФАЗА ИМПАКТА (Честный подлет к цели в ОЗУ)
                // =========================================================================
                if (distanceToTarget <= (movement.Speed * deltaTime) || distanceToTarget <= 0.5f)
                {
                    // 1. Вбрасываем отложенный пакет урона в боевую систему
                    Entity combatEventEntity = ecb.CreateEntity();
                    ecb.AddComponent(combatEventEntity, new CombatEventRequest
                    {
                        Caster = movement.CasterEntity,
                        Target = target,
                        AbilityId = movement.AbilityId
                    });

                    // 2. Транслируем визуальный сигнал Hit в шину презентации (чтобы сработал VFX взрыва льда)
                    if (eventBufferSingleton != Entity.Null)
                    {
                        // 🦾 СИ-ФИКС: Никаких "var presentationBuffer" и никаких "Add"! Передаем структуру сразу в аргументы!
                        ecb.AppendToBuffer<PresentationEvent>(eventBufferSingleton, new PresentationEvent
                        {
                            Kind = PresentationEventKind.Hit,
                            Source = movement.CasterEntity,
                            Target = target,
                            Param = movement.AbilityId
                        });
                    }

                    Debug.Log($"💥 [ProjectileSystem]: Снаряд '{movement.AbilityId}' попал в цель {target.Index}! Урон нанесен.");

                    // 3. Намертво уничтожаем Entity снаряда, прилет состоялся!
                    ecb.DestroyEntity(projectileEntity);
                    continue;
                }

                // =========================================================================
                // 🚀 ФАЗА ПОЛЕТА (Перемещение математической точки сквозь пространство)
                // =========================================================================
                float3 flightDirection = math.normalize(vectorToTarget);
                float3 frameMovement = flightDirection * movement.Speed * deltaTime;

                transformData.Position += frameMovement;
                transformData.Rotation = quaternion.LookRotation(flightDirection, new float3(0f, 1f, 0f));

                // Сохраняем обновленные физические координаты назад в чанк
                em.SetComponentData(projectileEntity, transformData);

                // Обновляем вектор направления движения в компоненте
                movement.Direction = flightDirection;
                em.SetComponentData(projectileEntity, movement);
            }

            projectileEntities.Dispose();
        }
    }
}


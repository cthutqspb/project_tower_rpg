using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Abilities;
using Debug = UnityEngine.Debug;
using quaternion = Unity.Mathematics.quaternion;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CastSystem))] 
    public partial class ProjectileSpawnSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton< EndSimulationEntityCommandBufferSystem.Singleton >().CreateCommandBuffer(World.Unmanaged);

            // Мгновенный перебор прилетевших квитанций
            foreach (var (requestData, requestEntity) in SystemAPI.Query< RefRO< ProjectileSpawnRequest > >().WithEntityAccess())
            {
                Entity caster = requestData.ValueRO.CasterEntity;
                Entity target = requestData.ValueRO.TargetEntity;

                if (!em.Exists(caster) || !em.Exists(target) || em.HasComponent< IsDeadTag >(target))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                string abilityIdStr = requestData.ValueRO.AbilityId.ToString();
                var abilityCfg = AbilitiesDatabase.GetAbility(abilityIdStr);

                // 🦾 СИ-ФИКС: Рождаем АБСОЛЮТНО ПУСТУЮ невидимую сущность снаряда в ОЗУ за 1 такт процессора!
                Entity projectileEntity = ecb.CreateEntity();

                // Вычисляем координаты спавна (уровень рук мага)
                var casterTransform = em.GetComponentData< LocalTransform >(caster);
                float3 spawnPosition = casterTransform.Position + new float3(0f, 1.2f, 0f);
                
                var targetTransform = em.GetComponentData< LocalTransform >(target);
                float3 vectorToTarget = targetTransform.Position - spawnPosition;
                vectorToTarget.y = 0f;
                float3 flightDirection = math.normalize(vectorToTarget);

                // Записываем физические координаты невидимой точки
                ecb.AddComponent(projectileEntity, new LocalTransform
                {
                    Position = spawnPosition,
                    Rotation = quaternion.LookRotation(flightDirection, new float3(0f, 1f, 0f)),
                    Scale = 1f
                });

                float projectileSpeed = (abilityCfg != null && abilityCfg.parameters != null && abilityCfg.parameters.speed > 0f) 
                    ? abilityCfg.parameters.speed 
                    : 18f;

                // Записываем геймплейную математику полета и ПУТЬ ПРЕФАБА
                ecb.AddComponent(projectileEntity, new ProjectileMovement
                {
                    Direction = flightDirection,
                    Speed = projectileSpeed,
                    TargetEntity = target,
                    CasterEntity = caster,
                    AbilityId = requestData.ValueRO.AbilityId,
                    PrefabPath = requestData.ValueRO.PrefabPath // 🦾 Переложили строку в снаряд!
                });


                ecb.AddComponent< ProjectileTag >(projectileEntity);

                Debug.Log($"🚀 [Server Projectile]: В ОЗУ сервера пущена невидимая точка '{abilityIdStr}' (Entity: {projectileEntity.Index})");

                // Сжигаем квитанцию приказа
                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}
